using Logic.Mapek.Proactive;
using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Monitoring;

// MAPE-K monitor that observes Home Assistant runtime state and the active
// price provider for /api/mapek/tick.
//
// Behavior:
//   * HA snapshot (issue #52) — IHaStateReader populates HaEntitiesSnapshot,
//     or we emit HaSnapshotUnavailableWarning on failure.
//   * Price observation (issue #53) — IPriceForecastProvider populates
//     PriceSource and CurrentPriceNokPerKwh (from the slot covering "now"). On
//     provider exceptions or Available=false we emit PriceProviderUnavailableWarning
//     plus the provider's own warning if any. We NEVER invent a price; the
//     caller can always tell whether observation succeeded or not.
//
// The monitor never actuates anything — it only reads existing services.
public sealed class MapekMonitorService : IMapekMonitorService
{
    internal const string PriceSourceUnavailable = "unavailable";

    internal const string HaSnapshotUnavailableWarning =
        "Home Assistant state observation is unavailable in this runtime path.";

    internal const string PriceProviderUnavailableWarning =
        "Price provider observation is unavailable in this runtime path.";

    private readonly IHaStateReader? _haStateReader;
    private readonly IPriceForecastProvider? _priceProvider;

    // All dependencies optional so offline tests and minimal setups keep
    // working — both fall through to the corresponding "unavailable" path.
    public MapekMonitorService(
        IHaStateReader? haStateReader = null,
        IPriceForecastProvider? priceProvider = null)
    {
        _haStateReader = haStateReader;
        _priceProvider = priceProvider;
    }

    public async Task<RuntimeState> ObserveAsync(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        IReadOnlyDictionary<string, object?>? snapshot = null;
        if (_haStateReader != null)
        {
            snapshot = await _haStateReader.ReadAllAsync(cancellationToken);
        }
        if (snapshot == null)
        {
            warnings.Add(HaSnapshotUnavailableWarning);
            snapshot = new Dictionary<string, object?>();
        }

        var (priceSource, currentPrice) = await ObservePriceAsync(warnings, cancellationToken);

        return new RuntimeState(
            HaEntitiesSnapshot: snapshot,
            CurrentPriceNokPerKwh: currentPrice,
            PriceSource: priceSource,
            Warnings: warnings);
    }

    private async Task<(string Source, double? CurrentPrice)> ObservePriceAsync(
        List<string> warnings, CancellationToken cancellationToken)
    {
        if (_priceProvider == null)
        {
            warnings.Add(PriceProviderUnavailableWarning);
            return (PriceSourceUnavailable, null);
        }

        PriceForecast? forecast;
        try
        {
            forecast = await _priceProvider.GetForecastAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Real providers can throw (e.g. ReplayLoadException on missing/malformed
            // file). We surface the failure as a warning and keep the price unset
            // rather than masking it with a fake value.
            warnings.Add(PriceProviderUnavailableWarning);
            warnings.Add($"Price provider error: {ex.Message}");
            return (PriceSourceUnavailable, null);
        }

        if (forecast == null)
        {
            warnings.Add(PriceProviderUnavailableWarning);
            return (PriceSourceUnavailable, null);
        }

        // Even when the provider returned Available=false, we keep its Source so
        // the consumer can tell which provider was attempted (e.g. nord pool vs
        // replay). The Warning, when present, is propagated verbatim.
        var source = string.IsNullOrWhiteSpace(forecast.Source) ? PriceSourceUnavailable : forecast.Source;

        if (!forecast.Available)
        {
            warnings.Add(PriceProviderUnavailableWarning);
            if (!string.IsNullOrWhiteSpace(forecast.Warning))
            {
                warnings.Add(forecast.Warning!);
            }
            return (source, null);
        }

        var currentPrice = FindCurrentSlotPrice(forecast.Slots, DateTimeOffset.UtcNow);
        return (source, currentPrice);
    }

    // Returns the price of the slot that contains `now` (Start <= now < End), or
    // null if no slot matches. We do NOT pick the nearest-future slot or any
    // surrogate — that would amount to inventing a current price.
    internal static double? FindCurrentSlotPrice(IReadOnlyList<PriceSlot> slots, DateTimeOffset now)
    {
        if (slots == null) return null;
        foreach (var slot in slots)
        {
            if (slot.Start <= now && now < slot.End) return slot.Price;
        }
        return null;
    }
}
