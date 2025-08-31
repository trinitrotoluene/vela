using Microsoft.Extensions.Options;
using Vela.Services.Contracts;

namespace Vela.Services.Impl;

public class MetricHelpers : IMetricHelpers
{
  private readonly IOptions<BitcraftServiceOptions> _options;

  public MetricHelpers(IOptions<BitcraftServiceOptions> options)
  {
    _options = options;
  }

  public string ServiceName => $"gateway-{_options.Value.Module}";
}