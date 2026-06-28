using Convergence.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vela.Contracts.Entities;
using Vela.Services.Impl;
using Xunit;

namespace Vela.Tests;

// Unit tests for the steady-state buffer/flush behaviour added to ConvergeDbWriter.
//
// Strategy: these exercise the writer's own control logic — arrival-order preservation,
// per-op metadata pairing, the snapshot-epoch gate, the empty-tick (dirty) skip, and
// transport-failure → host-shutdown — through a fake IConvergenceWriteSink. That seam is the
// boundary between Vela's logic and the ConvergeDB SDK; the SDK itself already guarantees that a
// flush sends buffered ops in append order with each op's metadata attached (single ordered
// List<BatchSubOp>, metadata copied per op at enqueue), so re-verifying the wire behaviour would
// need a live server and add no coverage of Vela code. Snapshot-path non-regression is covered
// structurally (those code paths are untouched) and behaviourally by the epoch-gate test, which
// proves the per-tick flush never fires while a snapshot epoch is open.
public class ConvergeDbWriterTests
{
    private static ConvergeDbWriter CreateWriter(FakeWriteSink sink, FakeHostApplicationLifetime lifetime)
        => new(NullLogger<ConvergeDbWriter>.Instance, lifetime, sink);

    [Fact]
    public async Task BufferedWrites_AreForwardedInArrivalOrder()
    {
        var sink = new FakeWriteSink();
        var writer = CreateWriter(sink, new FakeHostApplicationLifetime());

        await writer.AssertAsync(default(ConvergeBuildingState));
        await writer.AssertAsync(default(ConvergeClaimState));
        await writer.RetractAsync<ConvergeBuildingState>(EntityId.FromULong(7));

        Assert.Equal(
            new[]
            {
                ("assert", typeof(ConvergeBuildingState)),
                ("assert", typeof(ConvergeClaimState)),
                ("retract", typeof(ConvergeBuildingState)),
            },
            sink.Ops.Select(o => (o.Kind, o.Type)).ToArray());
    }

    [Fact]
    public async Task EachOp_KeepsItsOwnMetadata()
    {
        var sink = new FakeWriteSink();
        var writer = CreateWriter(sink, new FakeHostApplicationLifetime());

        var md1 = new EntityMetadata(("rd", "Build"));
        var md2 = new EntityMetadata(("rd", "Claim"));
        var md3 = new EntityMetadata(("rd", "Demolish"));

        await writer.AssertAsync(default(ConvergeBuildingState), md1);
        await writer.AssertAsync(default(ConvergeClaimState), md2);
        await writer.RetractAsync<ConvergeBuildingState>(EntityId.FromULong(7), md3);

        // Metadata stays paired with its own op (by reference — nothing is merged client-side).
        Assert.Same(md1, sink.Ops[0].Metadata);
        Assert.Same(md2, sink.Ops[1].Metadata);
        Assert.Same(md3, sink.Ops[2].Metadata);
    }

    [Fact]
    public async Task FlushPending_NoOps_WhileSnapshotEpochOpen()
    {
        var sink = new FakeWriteSink();
        var writer = CreateWriter(sink, new FakeHostApplicationLifetime());

        await writer.BeginEpochAsync();
        await writer.AssertAsync(default(ConvergeBuildingState)); // buffered during the epoch

        // Gated: a per-tick flush must not fire while the snapshot epoch owns the buffer.
        await writer.FlushPendingAsync(CancellationToken.None);
        await writer.FlushPendingAsync(CancellationToken.None);
        Assert.Equal(0, sink.FlushCount);

        // Once the epoch ends, the still-dirty buffer flushes on the next tick (ops not lost).
        await writer.EndEpochAsync();
        await writer.FlushPendingAsync(CancellationToken.None);
        Assert.Equal(1, sink.FlushCount);
    }

    [Fact]
    public async Task FlushPending_SkipsWhenNothingBuffered()
    {
        var sink = new FakeWriteSink();
        var writer = CreateWriter(sink, new FakeHostApplicationLifetime());

        // Idle tick: nothing buffered → no flush.
        await writer.FlushPendingAsync(CancellationToken.None);
        Assert.Equal(0, sink.FlushCount);

        // One write makes the writer dirty → exactly one flush, and the dirty flag is cleared.
        await writer.AssertAsync(default(ConvergeBuildingState));
        await writer.FlushPendingAsync(CancellationToken.None);
        await writer.FlushPendingAsync(CancellationToken.None);
        Assert.Equal(1, sink.FlushCount);
    }

    [Fact]
    public async Task FlushPending_TransportFailure_StopsApplication()
    {
        var sink = new FakeWriteSink { FlushThrows = () => new IOException("simulated transport failure") };
        var lifetime = new FakeHostApplicationLifetime();
        var writer = CreateWriter(sink, lifetime);

        await writer.AssertAsync(default(ConvergeBuildingState));

        // Transport failures are swallowed after signalling host shutdown (Docker restarts → re-seed).
        await writer.FlushPendingAsync(CancellationToken.None);

        Assert.Equal(1, sink.FlushCount);
        Assert.Equal(1, lifetime.StopApplicationCalls);
    }

    [Fact]
    public async Task FlushPending_NonTransportFailure_PropagatesAndDoesNotStop()
    {
        var sink = new FakeWriteSink { FlushThrows = () => new InvalidOperationException("not a transport error") };
        var lifetime = new FakeHostApplicationLifetime();
        var writer = CreateWriter(sink, lifetime);

        await writer.AssertAsync(default(ConvergeBuildingState));

        // Non-transport faults are not classified as a transport failure: they propagate and must
        // not trigger a host shutdown.
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.FlushPendingAsync(CancellationToken.None));
        Assert.Equal(0, lifetime.StopApplicationCalls);
    }

    private sealed class FakeWriteSink : IConvergenceWriteSink
    {
        public List<RecordedOp> Ops { get; } = new();
        public int FlushCount { get; private set; }
        public Func<Exception>? FlushThrows { get; set; }

        public ValueTask AssertAsync<T>(T entity, EntityMetadata? metadata) where T : struct, IConvergenceEntity<T>
        {
            Ops.Add(new RecordedOp("assert", typeof(T), metadata));
            return ValueTask.CompletedTask;
        }

        public ValueTask RetractAsync<T>(ReadOnlyMemory<byte> entityId, EntityMetadata? metadata) where T : struct, IConvergenceEntity<T>
        {
            Ops.Add(new RecordedOp("retract", typeof(T), metadata));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken ct)
        {
            FlushCount++;
            var ex = FlushThrows?.Invoke();
            if (ex != null) throw ex;
            return ValueTask.CompletedTask;
        }

        public Task EpochBeginAsync(CancellationToken ct) => Task.CompletedTask;
        public Task EpochEndAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed record RecordedOp(string Kind, Type Type, EntityMetadata? Metadata);

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public int StopApplicationCalls { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopApplicationCalls++;
    }
}
