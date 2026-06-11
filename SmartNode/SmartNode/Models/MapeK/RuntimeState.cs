namespace SmartNode.Models.MapeK;

public record RuntimeState(
    IReadOnlyDictionary<string, object?> HaEntitiesSnapshot,
    double? CurrentPriceNokPerKwh,
    string PriceSource,
    IReadOnlyList<string> Warnings
);
