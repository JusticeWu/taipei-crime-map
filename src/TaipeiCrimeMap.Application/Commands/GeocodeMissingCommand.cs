namespace TaipeiCrimeMap.Application.Commands;

public record GeocodeMissingCommand(int? MaxCount, int DelayBetweenCallsMs = 50);
