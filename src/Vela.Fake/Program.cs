using System.Runtime.InteropServices;
using Vela.Fake;

// Stands in for Vela's gateways: publishes made-up game state in Vela's contract to ConvergeDB, so
// consumers can be smoke-tested against it. See README.md.
FakeOptions options;
try
{
    options = FakeOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(FakeOptions.Usage);
    return 2;
}

if (options.Help)
{
    Console.WriteLine(FakeOptions.Usage);
    return 0;
}

using var cts = new CancellationTokenSource();
using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);

void Stop(PosixSignalContext context)
{
    context.Cancel = true;
    cts.Cancel();
}

try
{
    if (options.Postgres is { } postgres) await Descriptors.SeedAsync(postgres, options.Seed, cts.Token);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    return 0;
}

await Task.WhenAll(options.Regions.Select(n => new Region(n, options).RunAsync(cts.Token)));
return 0;
