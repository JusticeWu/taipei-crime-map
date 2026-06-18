using System.IO;
using System.Reflection;
using Microsoft.Data.SqlClient;
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
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        EnsureSchemaVersionsTable(conn);

        var applied = GetAppliedScripts(conn);
        var scripts = GetEmbeddedScripts();

        var anyRun = false;
        foreach (var (name, sql) in scripts)
        {
            if (applied.Contains(name)) continue;

            _logger.LogInformation("執行 Migration：{Script}", name);
            ExecuteScript(conn, sql);
            RecordScript(conn, name);
            anyRun = true;
        }

        if (!anyRun)
            _logger.LogInformation("資料庫已是最新版本，無需 Migration。");
        else
            _logger.LogInformation("Migration 成功完成。");
    }

    private static void EnsureSchemaVersionsTable(SqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'schemaversions')
            CREATE TABLE schemaversions (
                id         INT IDENTITY(1,1) PRIMARY KEY,
                scriptname NVARCHAR(500) NOT NULL,
                applied    DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private static HashSet<string> GetAppliedScripts(SqlConnection conn)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT scriptname FROM schemaversions";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            applied.Add(reader.GetString(0));
        return applied;
    }

    private static List<(string Name, string Sql)> GetEmbeddedScripts()
    {
        var assembly = typeof(DbUpMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return (name, reader.ReadToEnd());
            })
            .ToList();
    }

    private static void ExecuteScript(SqlConnection conn, string sql)
    {
        var batches = System.Text.RegularExpressions.Regex
            .Split(sql, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (trimmed.Length == 0) continue;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = trimmed;
            cmd.CommandTimeout = 60;
            cmd.ExecuteNonQuery();
        }
    }

    private static void RecordScript(SqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO schemaversions (scriptname) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }
}
