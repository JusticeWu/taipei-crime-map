using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaipeiCrimeMap.Infrastructure.Persistence;

namespace TaipeiCrimeMap.Integration.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminUsername = "test-admin";
    public const string AdminPassword = "test-password";

    private static readonly SemaphoreSlim _migrationLock = new(1, 1);
    private static bool _migrated = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAuth:Username"] = AdminUsername,
                ["AdminAuth:Password"] = AdminPassword,
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        _migrationLock.Wait();
        try
        {
            if (!_migrated)
            {
                var migrator = host.Services.GetRequiredService<DbUpMigrator>();
                migrator.MigrateUp();
                _migrated = true;
            }
        }
        finally
        {
            _migrationLock.Release();
        }

        return host;
    }
}