using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Infrastructure.Csv;
using TaipeiCrimeMap.Infrastructure.Geocoding;
using TaipeiCrimeMap.Infrastructure.Persistence;
using TaipeiCrimeMap.Infrastructure.Repositories;

namespace TaipeiCrimeMap.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options pattern for Google Maps API settings 
        services.Configure<GoogleMapsOptions>(configuration.GetSection(GoogleMapsOptions.SectionName));

        // 使用 HttpClientFactory 建立 IGeocodingService，並套用 Polly 的重試和熔斷策略
        services.AddHttpClient<IGeocodingService, GoogleGeocodingService>().AddStandardResilienceHandler();

        // Repository
        services.AddSingleton<ICrimeRepository, InMemoryCrimeRepository>();

        services.AddScoped<DbUpMigrator>(sp =>
            new DbUpMigrator(
                configuration.GetConnectionString("DefaultConnection")!,
                sp.GetRequiredService<ILogger<DbUpMigrator>>()));

        // CSV
        services.AddSingleton<ICsvParser, CsvParser>();

        return services;
    }
}