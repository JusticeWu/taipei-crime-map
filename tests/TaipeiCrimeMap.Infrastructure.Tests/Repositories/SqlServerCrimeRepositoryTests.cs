using System.Diagnostics;
using FluentAssertions;
using TaipeiCrimeMap.Domain.ValueObjects;
using TaipeiCrimeMap.Infrastructure.Repositories;

namespace TaipeiCrimeMap.Infrastructure.Tests.Repositories;

public class SqlServerCrimeRepositoryTests
{
    [Fact]
    public async Task GetStatsByFilterAsync_RunsDistrictAndTimeSlotQueriesInParallel()
    {
        // 連到不可路由的位址（10.255.255.1），連線會在 Connect Timeout 後逾時失敗。
        // 兩個查詢各自使用獨立連線：若並行執行，兩個逾時是同時發生，
        // 總耗時應接近單次逾時時間；若是串行執行，總耗時應接近兩次逾時相加。
        const string connectionString =
            "Server=10.255.255.1,1433;Connect Timeout=1;TrustServerCertificate=True;Encrypt=False";

        var repository = new SqlServerCrimeRepository(connectionString);
        var filter = new CrimeFilter();

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.GetStatsByFilterAsync(filter));
        stopwatch.Stop();

        // 並行：約 1 秒；串行：約 2 秒以上。取 1.8 秒作為門檻。
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1.8));
    }
}
