namespace TaipeiCrimeMap.Application.DTOs;

public record DistrictDistributionDto(string District, int Count);

public record TimeSlotDistributionDto(string TimeSlot, int Count);

public record CrimeStatsDto(
    IReadOnlyList<DistrictDistributionDto> DistrictDistribution,
    IReadOnlyList<TimeSlotDistributionDto> TimeSlotDistribution);
