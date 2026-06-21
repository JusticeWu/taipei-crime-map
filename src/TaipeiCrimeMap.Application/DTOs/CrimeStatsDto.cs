namespace TaipeiCrimeMap.Application.DTOs;

public record DistrictDistributionDto(string District, int Count);

public record TimeSlotDistributionDto(string TimeSlot, int Count);

public record TrendSeriesDto(string Label, IReadOnlyList<TrendPointDto> Points);

public record TrendPointDto(int Year, int Count);

public record CrimeStatsDto(
    IReadOnlyList<DistrictDistributionDto> DistrictDistribution,
    IReadOnlyList<TimeSlotDistributionDto> TimeSlotDistribution,
    IReadOnlyList<TrendSeriesDto> TimeSlotCaseTypeTrend,
    IReadOnlyList<TrendSeriesDto> DistrictTimeSlotTrend,
    IReadOnlyList<TrendSeriesDto> DistrictCaseTypeTrend);
