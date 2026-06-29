using Convergence.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Vela.Contracts.Entities;
using Vela.Services.Contracts;
using Vela.Services.Impl;
using Xunit;

namespace Vela.Tests;

// Unit tests for the steady-state buffer/flush behaviour added to ConvergeDbWriter.
//
// Strategy: these exercise the writer's own control logic - arrival-order preservation,
// per-op metadata pairing, the snapshot-epoch gate, the empty-tick (dirty) skip, and
// ConvergeDB-failure → fatal restart - through a fake IConvergenceWriteSink. That seam is the
// boundary between Vela's logic and the ConvergeDB SDK; the SDK itself already guarantees that a
// flush sends buffered ops in append order with each op's metadata attached (single ordered
// List<BatchSubOp>, metadata copied per op at enqueue), so re-verifying the wire behaviour would
// need a live server and add no coverage of Vela code. Snapshot-path non-regression is covered
// structurally (those code paths are untouched) and behaviourally by the epoch-gate test, which
// proves the per-tick flush never fires while a snapshot epoch is open.
//
// Restart policy under test: every ConvergeDB I/O failure (flush, begin/end epoch), regardless of
// exception type, must request a process restart via IFatalRestart and be swallowed - never
// propagated into the connection loop where it could be mistaken for a recoverable SpacetimeDB blip.
// The one exception is cancellation (normal shutdown unwinding), which must propagate and NOT restart.
public class ConvergeDbWriterTests
{
    private static ConvergeDbWriter CreateWriter(FakeWriteSink sink, FakeFatalRestart fatal)
        => new(NullLogger<ConvergeDbWriter>.Instance, fatal, sink);

    [Fact]
    public async Task BufferedWrites_AreForwardedInArrivalOrder()
    {
        var sink = new FakeWriteSink();
        var writer = CreateWriter(sink, new FakeFatalRestart());

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
        var writer = CreateWriter(sink, new FakeFatalRestart());

        var md1 = new EntityMetadata(("rd", "Build"));
        var md2 = new EntityMetadata(("rd", "Claim"));
        var md3 = new EntityMetadata(("rd", "Demolish"));

        await writer.AssertAsync(default(ConvergeBuildingState), md1);
        await writer.AssertAsync(default(ConvergeClaimState), md2);
        await writer.RetractAsync<ConvergeBuildingState>(EntityId.FromULong(7), md3);

        // Metadata stays paired with its own op (by reference - nothing is merged client-side).
        Assert.Same(md1, sink.Ops[0].Metadata);
        Assert.Same(md2, sink.Ops[1].Metadata);
        Assert.Same(md3, sink.Ops[2].Metadata);
    }

    [Fact]
    public async Task FlushPending_NoOps_WhileSnapshotEpochOpen()
    {
        var sink = new FakeWriteSink();
        var writer = CreateWriter(sink, new FakeFatalRestart());

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
        var writer = CreateWriter(sink, new FakeFatalRestart());

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
    public async Task FlushPending_TransportFailure_TriggersRestart()
    {
        var sink = new FakeWriteSink { FlushThrows = () => new IOException("simulated transport failure") };
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.AssertAsync(default(ConvergeBuildingState));

        // ConvergeDB failures are swallowed after signalling a restart (Docker restarts → re-seed).
        await writer.FlushPendingAsync(CancellationToken.None);

        Assert.Equal(1, sink.FlushCount);
        Assert.Equal(1, fatal.TriggerCalls);
    }

    [Fact]
    public async Task FlushPending_NotConnected_TriggersRestart()
    {
        // Regression: when the ConvergeDB connection drops, ConnectionManager.FlushAsync throws a
        // bare InvalidOperationException("Not connected.") from its `_transport == null` guard - not
        // a SocketException/IOException. A previous type-based classifier failed to recognise this,
        // so the restart path was skipped and the host hung instead of restarting. Any non-cancellation
        // flush failure must now signal a restart, whatever its type.
        var sink = new FakeWriteSink { FlushThrows = () => new InvalidOperationException("Not connected.") };
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.AssertAsync(default(ConvergeBuildingState));

        // Swallowed after signalling the restart; must not propagate into the connection loop, where
        // it would be mistaken for a recoverable SpacetimeDB blip.
        await writer.FlushPendingAsync(CancellationToken.None);

        Assert.Equal(1, sink.FlushCount);
        Assert.Equal(1, fatal.TriggerCalls);
    }

    [Fact]
    public async Task FlushPending_WhileShuttingDown_PropagatesAndDoesNotRestart()
    {
        // When OUR token is cancelled the host is stopping: whatever the flush throws must propagate
        // and unwind (so the connection loop's cancellation handling runs) and must NOT trigger a
        // restart - a clean, intentional shutdown should never be escalated into a failure exit.
        var sink = new FakeWriteSink { FlushThrows = () => new OperationCanceledException() };
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.AssertAsync(default(ConvergeBuildingState));

        var cancelled = new CancellationToken(canceled: true);
        await Assert.ThrowsAsync<OperationCanceledException>(() => writer.FlushPendingAsync(cancelled));
        Assert.Equal(0, fatal.TriggerCalls);
    }

    [Fact]
    public async Task FlushPending_CancelledException_ButNotShuttingDown_TriggersRestart()
    {
        // A mid-request ConvergeDB drop can surface as an OperationCanceledException from the client's
        // OWN internal token even though we never cancelled. That is still a fatal transport failure,
        // not shutdown: because our token is not cancelled it must restart. (A type-based filter that
        // excluded all OperationCanceledExceptions would have silently mistaken this for shutdown.)
        var sink = new FakeWriteSink { FlushThrows = () => new OperationCanceledException() };
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.AssertAsync(default(ConvergeBuildingState));

        await writer.FlushPendingAsync(CancellationToken.None);
        Assert.Equal(1, fatal.TriggerCalls);
    }

    [Fact]
    public async Task BeginEpoch_NotConnected_TriggersRestart()
    {
        // Same "Not connected." failure surfacing from the snapshot's epoch-open call: also fatal.
        var sink = new FakeWriteSink { EpochBeginThrows = () => new InvalidOperationException("Not connected.") };
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.BeginEpochAsync();

        Assert.Equal(1, fatal.TriggerCalls);
    }

    [Fact]
    public async Task EndEpoch_NotConnected_TriggersRestart()
    {
        var sink = new FakeWriteSink { EpochEndThrows = () => new InvalidOperationException("Not connected.") };
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.BeginEpochAsync();
        await writer.EndEpochAsync();

        Assert.Equal(1, fatal.TriggerCalls);
    }

    [Fact]
    public async Task BeginEpoch_DoubleOpen_ThrowsAndDoesNotRestart()
    {
        // Double-begin is a Vela logic error, not a ConvergeDB I/O failure: it fails fast (throws)
        // so the bug surfaces, rather than bouncing the container.
        var sink = new FakeWriteSink();
        var fatal = new FakeFatalRestart();
        var writer = CreateWriter(sink, fatal);

        await writer.BeginEpochAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.BeginEpochAsync());
        Assert.Equal(0, fatal.TriggerCalls);
    }

    private sealed class FakeWriteSink : IConvergenceWriteSink
    {
        public List<RecordedOp> Ops { get; } = new();
        public int FlushCount { get; private set; }
        public Func<Exception>? FlushThrows { get; set; }
        public Func<Exception>? EpochBeginThrows { get; set; }
        public Func<Exception>? EpochEndThrows { get; set; }

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

        public Task EpochBeginAsync(CancellationToken ct)
        {
            var ex = EpochBeginThrows?.Invoke();
            return ex != null ? Task.FromException(ex) : Task.CompletedTask;
        }

        public Task EpochEndAsync(CancellationToken ct)
        {
            var ex = EpochEndThrows?.Invoke();
            return ex != null ? Task.FromException(ex) : Task.CompletedTask;
        }
    }

    private sealed record RecordedOp(string Kind, Type Type, EntityMetadata? Metadata);

    private sealed class FakeFatalRestart : IFatalRestart
    {
        public int TriggerCalls { get; private set; }
        public string? LastReason { get; private set; }
        public void Trigger(string reason)
        {
            TriggerCalls++;
            LastReason = reason;
        }
    }
}
