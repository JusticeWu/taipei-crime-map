using DbUp;
using Microsoft.Extensions.Logging;

namespace TaipeiCrimeMap.Infrastructure.Persistence;

public class DbUpMigrator
{
    private readonly string _connectionString;
    private readonly ILogger<DbUpMigrator> _logger;

    public DbUpMigrator(string connectionString, ILogger<DbUpMigrator> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public void MigrateUp()
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DbUpMigrator).Assembly)
            .LogToConsole()
            .Build();

        if (!upgrader.IsUpgradeRequired())
        {
            _logger.LogInformation("資料庫已是最新版本，無需 Migration。");
            return;
        }

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            _logger.LogError(result.Error, "Migration 失敗：{Script}", result.ErrorScript?.Name);
            throw new Exception($"Migration 失敗：{result.Error.Message}");
        }

        _logger.LogInformation("Migration 成功完成。");
    }
}
