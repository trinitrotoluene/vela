namespace Vela.Fake;

public sealed record FakeOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 3727;
    public IReadOnlyList<int> Regions { get; init; } = [1];
    public int Scale { get; init; } = 10_000;
    public int Rate { get; init; } = 200;
    public int Seed { get; init; } = 1;
    public int RestartEverySeconds { get; init; }
    public double RestartDrop { get; init; } = 0.05;
    public int ReportEverySeconds { get; init; } = 30;
    public string? Postgres { get; init; }
    public bool Help { get; init; }

    public const string Usage = """
        Usage: Vela.Fake [options]
          --host <host>           ConvergeDB host (default localhost)
          --port <port>           ConvergeDB port (default 3727)
          --regions <n,n,...>     regions to publish, each a source id 1-255 (default 1)
          --scale <n>             rows in each large kind (inventories, buildings) per region (default 10000)
          --rate <n>              live changes per second per region (default 200)
          --seed <n>              random seed (default 1)
          --restart-every <s>     reconnect and publish a new snapshot every s seconds, as a Vela restart does (default off)
          --restart-drop <f>      fraction of inventories, orders, listings and crafts left out of each new snapshot (default 0.05)
          --report-every <s>      log row counts every s seconds (default 30)
          --postgres <connstr>    first migrate this Vela database and fill it with descriptors for the fake's ids (default off)
        """;

    public static FakeOptions Parse(string[] args)
    {
        var o = new FakeOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i];
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{name} needs a value");
            int Int() => int.TryParse(Value(), out var v) && v >= 0 ? v : throw new ArgumentException($"{name} needs a whole number");
            o = name switch
            {
                "--host" => o with { Host = Value() },
                "--port" => o with { Port = Int() },
                "--regions" => o with { Regions = ParseRegions(Value()) },
                "--scale" => o with { Scale = Math.Max(100, Int()) },
                "--rate" => o with { Rate = Int() },
                "--seed" => o with { Seed = Int() },
                "--restart-every" => o with { RestartEverySeconds = Int() },
                "--restart-drop" => o with
                {
                    RestartDrop = double.TryParse(Value(), System.Globalization.CultureInfo.InvariantCulture, out var f) && f is >= 0 and <= 1
                        ? f
                        : throw new ArgumentException("--restart-drop needs a fraction between 0 and 1"),
                },
                "--report-every" => o with { ReportEverySeconds = Math.Max(1, Int()) },
                "--postgres" => o with { Postgres = Value() },
                "--help" or "-h" => o with { Help = true },
                _ => throw new ArgumentException($"Unknown option {name}"),
            };
        }
        return o;
    }

    private static int[] ParseRegions(string list)
    {
        var regions = list.Split(',').Select(ParseRegion).ToArray();
        // Two connections with one source id would retract each other's rows.
        return regions.Distinct().Count() == regions.Length ? regions : throw new ArgumentException("--regions lists a region twice");
    }

    private static int ParseRegion(string s) =>
        int.TryParse(s, out var n) && n is >= 1 and <= 255 ? n : throw new ArgumentException($"Region '{s}' must be 1-255");
}
