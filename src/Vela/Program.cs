using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Vela.Data;
using Vela.Services.Contracts;
using Vela.Services.Impl;
using Convergence.Client;

var builder = Host.CreateApplicationBuilder();

var env = Environment.GetEnvironmentVariable("ENV") ?? "Development";
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables("VELA_");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(options =>
    {
        var overrideHost = builder.Configuration.GetValue<string>("Otel:Endpoint");
        if (!string.IsNullOrEmpty(overrideHost))
        {
            Console.WriteLine($"Shipping logs to custom endpoint {overrideHost}");
            options.Endpoint = overrideHost;
            options.Protocol = OtlpProtocol.HttpProtobuf;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = $"gateway-{builder.Configuration.GetValue<string>("Bitcraft:Module")}"
            };
        }
        else
        {
            Console.WriteLine("Using default log shipper");
        }
    })
    .CreateLogger();

builder.Logging.ClearProviders().AddSerilog();

builder.Services.AddJsonSerializer();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(x => x.AddService(builder.Configuration.GetValue<string>("Bitcraft:Module")!))
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddMeter("Vela")
            .AddOtlpExporter((options, readerOptions) =>
            {
                var overrideHost = builder.Configuration.GetValue<string>("Otel:Endpoint");
                if (!string.IsNullOrEmpty(overrideHost))
                {
                    Log.Logger.Information("Shipping metrics to custom endpoint {endpoint}", overrideHost);
                    options.Endpoint = new Uri($"{overrideHost}/v1/metrics");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;

                    readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                }
                else
                {
                    Log.Logger.Information("Using default metric shipper");
                }
            });
    });

// PostgreSQL - descriptor entities only (items, recipes, building descs, etc.)
builder.Services.AddDbContextFactory<VelaDbContext>(options =>
{
    var connString = new NpgsqlConnectionStringBuilder(
        builder.Configuration.GetConnectionString("VelaDb"))
    {
        MaxPoolSize = 10,
        Timeout = 30
    };
    options.UseNpgsql(connString.ConnectionString)
           .UseSnakeCaseNamingConvention()
           .EnableSensitiveDataLogging(false);
});

// ConvergeDB - dynamic game state entities
builder.Services.AddOptions<ConvergeDbOptions>()
    .Bind(builder.Configuration.GetSection("ConvergeDb"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IConvergeDbWriter, ConvergeDbWriter>();
builder.Services.AddSingleton<IDescriptorDbWriter, DescriptorDbWriter>();
builder.Services.AddSingleton<IDbConnectionAccessor, DbConnectionAccessor>();
builder.Services.AddSingleton<IEventSubscriber, EventSubscriberService>();
builder.Services.AddSingleton<IMetricHelpers, MetricHelpers>();

builder.Services.AddHttpClient("Heartbeat", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddOptions<BitcraftServiceOptions>()
    .Bind(builder.Configuration.GetSection("Bitcraft"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<BitcraftService>();
builder.Services.AddHostedService<EventGatewayService>();

// Any unhandled exception in a BackgroundService stops the host → non-zero exit →
// Docker's restart policy re-runs the startup path (including the ConvergeDB
// EpochAsync-wrapped populate that cleanly re-seeds state from scratch).
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);

var app = builder.Build();

// Schema setup - run before hosted services start
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IDbContextFactory<VelaDbContext>>().CreateDbContext();
    await dbContext.Database.MigrateAsync();
}

var convergeWriter = app.Services.GetRequiredService<IConvergeDbWriter>();
await convergeWriter.InitializeAsync(CancellationToken.None);

await app.RunAsync();
