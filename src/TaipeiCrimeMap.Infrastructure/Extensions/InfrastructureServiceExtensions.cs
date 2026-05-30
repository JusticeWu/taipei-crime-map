using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Options pattern for Google Maps API settings 
        services.Configure<GoogleMapsOptions>(configuration.GetSection(GoogleMapsOptions.SectionName));

        // 使用 HttpClientFactory 建立 IGeocodingService，並套用 Polly 的重試和熔斷策略
        services.AddHttpClient<IGeocodingService, GoogleGeocodingService>().AddStandardResilienceHandler();

        // Repository
        services.AddSingleton<ICrimeRepository, InMemoryCrimeRepository>();

        // CSV
        services.AddSingleton<ICsvParser, CsvParser>();

        return services;
    }
}