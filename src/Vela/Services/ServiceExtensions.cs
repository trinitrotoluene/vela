using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceExtensions
{
  public static IServiceCollection AddJsonSerializer(this IServiceCollection services)
  {
    var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
    {
      Converters = {
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
      }
    };
    services.AddSingleton(jsonOptions);
    return services;
  }
}