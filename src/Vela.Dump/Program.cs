using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Vela.Dump.Services;
using Vela.Services.Contracts;
using Vela.Services.Impl;

Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT",
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development");

var builder = Host.CreateApplicationBuilder();

builder.Configuration.AddEnvironmentVariables("VELA_DUMP_");

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders().AddSerilog();

builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    IncludeFields = true,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
});
builder.Services.AddSingleton<IDbConnectionAccessor, DbConnectionAccessor>();
builder.Services.AddSingleton<IMetricHelpers, MetricHelpers>();
builder.Services.AddSingleton<JsonDumpSubscriber>();

builder.Services.AddOptions<BitcraftServiceOptions>()
    .Bind(builder.Configuration.GetSection("Bitcraft"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JsonDumpOptions>()
    .Bind(builder.Configuration.GetSection("Dump"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<BitcraftService>();
builder.Services.AddHostedService<JsonDumpGateway>();

var app = builder.Build();
await app.RunAsync();
