using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
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
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        // DbUp Migration
        services.AddSingleton(sp => new DbUpMigrator(connectionString, sp.GetRequiredService<ILogger<DbUpMigrator>>()));

        // Repository
        services.AddSingleton<ICrimeRepository>(_ => new SqlServerCrimeRepository(connectionString));

        // // Repository
        // services.AddSingleton<ICrimeRepository, InMemoryCrimeRepository>();

        // Options pattern for Google Maps API settings 
        services.Configure<GoogleMapsOptions>(configuration.GetSection(GoogleMapsOptions.SectionName));

        // 使用 HttpClientFactory 建立 IGeocodingService，並套用 Polly 的重試和熔斷策略
        services.AddHttpClient<IGeocodingService, GoogleGeocodingService>().AddStandardResilienceHandler();

        // CSV
        services.AddSingleton<ICsvParser, CsvParser>();

        // 分散式快取（Garnet / Redis）
        // ConnectTimeout/SyncTimeout 設為 2000ms：Garnet 失敗時快速 fallback 到 DB，
        // 不要讓單一請求被卡住 16~21 秒（cache-aside 模式下快取應為非強依賴）
        services.AddStackExchangeRedisCache(options =>
        {
            var redisConfigOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")!);
            redisConfigOptions.ConnectTimeout = 2000;
            redisConfigOptions.SyncTimeout = 2000;
            options.ConfigurationOptions = redisConfigOptions;
        });

        // 提供 IConnectionMultiplexer，供管理端點（清除快取 FLUSHALL）直接操作 Garnet
        // AbortOnConnectFail = false：Garnet 連線失敗時不讓應用程式啟動失敗
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConfigOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")!);
            redisConfigOptions.ConnectTimeout = 2000;
            redisConfigOptions.SyncTimeout = 2000;
            redisConfigOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisConfigOptions);
        });

        return services;
    }
}