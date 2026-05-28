using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Infrastructure.Repositories;

namespace TaipeiCrimeMap.Integration.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public readonly InMemoryCrimeRepository Reponsitory = new();
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 移除原本註冊的 ICrimeRepository
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICrimeRepository));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // 注入固定的 instance，確保所有測試共用同一個 Repository 
            services.AddSingleton<ICrimeRepository>(Reponsitory);
        });
    }
}