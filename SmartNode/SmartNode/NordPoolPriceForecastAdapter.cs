using Logic.Mapek.Proactive;
using Microsoft.Extensions.Logging;

namespace SmartNode;

// Bridges the Logic-layer IPriceForecastProvider contract to the SmartNode-only
// NordPoolForecastProvider, so MAPE-K can consume Nord Pool prices without
// Logic taking a direct dependency on the SmartNode assembly (which would be a cycle).
internal sealed class NordPoolPriceForecastAdapter : IPriceForecastProvider
{
    private readonly ILogger<NordPoolPriceForecastAdapter> _logger;

    public NordPoolPriceForecastAdapter(ILogger<NordPoolPriceForecastAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<PriceForecast> GetForecastAsync(CancellationToken ct = default)
    {
        var token = Environment.GetEnvironmentVariable("TOKEN_HA") ?? string.Empty;
        var forecast = await NordPoolForecastProvider.GetForecastAsync(token, _logger, ct);

        return new PriceForecast {
            Available = forecast.ForecastAvailable,
            Source = forecast.Source,
            Area = forecast.Area,
            Currency = forecast.Currency,
            Timezone = forecast.Timezone,
            Slots = forecast.Slots
                .Select(s => new PriceSlot(s.Start, s.End, s.Price))
                .ToList(),
            Warning = forecast.Warning
        };
    }
}
