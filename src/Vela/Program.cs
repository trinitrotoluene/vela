using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Vela.Data;
using Vela.Services.Contracts;
using Vela.Services.Impl;

var builder = Host.CreateApplicationBuilder();

var env = Environment.GetEnvironmentVariable("ENV") ?? "Development";
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{env}.json", optional: true)
    .AddEnvironmentVariables("VELA_");

Log.Logger = new LoggerConfiguration()
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

builder.AddRedisClient("Valkey");
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

builder.Services.AddDbContextFactory<VelaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("VelaDb"))
           .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<IEntityDbWriter, EntityDbWriter>();
builder.Services.AddSingleton<IDbConnectionAccessor, DbConnectionAccessor>();
builder.Services.AddSingleton<IEventSubscriber, EventSubscriberService>();
builder.Services.AddSingleton<IMetricHelpers, MetricHelpers>();

builder.Services.AddOptions<BitcraftServiceOptions>()
    .Bind(builder.Configuration.GetSection("Bitcraft"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<BitcraftService>();
builder.Services.AddHostedService<EventGatewayService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IDbContextFactory<VelaDbContext>>().CreateDbContext();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();