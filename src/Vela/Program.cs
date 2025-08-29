using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;

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
    .WriteTo.OpenTelemetry()
    .CreateLogger();

builder.Logging.AddSerilog();

builder.AddRedisClient("Valkey");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(x => x.AddService(builder.Configuration.GetValue<string>("Bitcraft:Module")!))
    .WithMetrics(x => x.AddConsoleExporter().AddOtlpExporter());

builder.Services.AddSingleton<IDbConnectionAccessor, DbConnectionAccessor>();
builder.Services.AddSingleton<IEventSubscriber, EventSubscriberService>();

builder.Services.AddOptions<BitcraftServiceOptions>()
    .Bind(builder.Configuration.GetSection("Bitcraft"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<BitcraftService>();
builder.Services.AddHostedService<EventGatewayService>();

var app = builder.Build();
await app.RunAsync();