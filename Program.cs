// See https://aka.ms/new-console-template for more information
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var host = new HostBuilder()
  .ConfigureAppConfiguration(configBuilder =>
  {
    configBuilder
      .AddJsonFile("appsettings.json", true)
      .AddEnvironmentVariables("VELA_");

    var env = Environment.GetEnvironmentVariable("ENV") ?? "Development";
    configBuilder.AddJsonFile($"appsettings.{env}.json", true);
  })
  .ConfigureLogging(logger =>
  {
    Log.Logger = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .CreateLogger();

    logger.AddSerilog();
  })
  .ConfigureServices((ctx, services) =>
  {

    services.AddOptions<BitcraftServiceOptions>()
      .Bind(ctx.Configuration.GetSection("Bitcraft"))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    services.AddHostedService<BitcraftService>();
  })
  .Build();

var config = host.Services.GetRequiredService<IConfiguration>();

await host.StartAsync();