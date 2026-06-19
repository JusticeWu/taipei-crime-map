using FluentAssertions;
using TaipeiCrimeMap.Infrastructure.Jobs;

namespace TaipeiCrimeMap.Infrastructure.Tests.Jobs;

public class AdaptiveConcurrencyControllerTests
{
    [Fact]
    public void OnSuccess_IncreasesCurrentLimitByOne()
    {
        var cc = new AdaptiveConcurrencyController(5, minLimit: 1, maxLimit: 10);
        cc.OnSuccess();
        cc.CurrentLimit.Should().Be(6);
    }

    [Fact]
    public void OnSuccess_DoesNotExceedMaxLimit()
    {
        var cc = new AdaptiveConcurrencyController(10, minLimit: 1, maxLimit: 10);
        cc.OnSuccess();
        cc.CurrentLimit.Should().Be(10);
    }

    [Fact]
    public void OnSuccess_ReachesMaxLimit_Gradually()
    {
        var cc = new AdaptiveConcurrencyController(8, minLimit: 1, maxLimit: 10);
        for (var i = 0; i < 5; i++) cc.OnSuccess();
        cc.CurrentLimit.Should().Be(10);
    }

    [Fact]
    public void OnTransientFailure_HalvesCurrentLimit()
    {
        var cc = new AdaptiveConcurrencyController(10, minLimit: 1, maxLimit: 20);
        cc.OnTransientFailure();
        cc.CurrentLimit.Should().Be(5);
    }

    [Fact]
    public void OnTransientFailure_DoesNotGoBelowMinLimit()
    {
        var cc = new AdaptiveConcurrencyController(2, minLimit: 1, maxLimit: 10);
        cc.OnTransientFailure();
        cc.CurrentLimit.Should().Be(1);
        cc.OnTransientFailure();
        cc.CurrentLimit.Should().Be(1);
    }

    [Fact]
    public void OnTransientFailure_ConsecutiveHalving()
    {
        var cc = new AdaptiveConcurrencyController(16, minLimit: 1, maxLimit: 20);
        cc.OnTransientFailure(); // 8
        cc.OnTransientFailure(); // 4
        cc.OnTransientFailure(); // 2
        cc.OnTransientFailure(); // 1
        cc.CurrentLimit.Should().Be(1);
    }

    [Fact(Timeout = 15000)]
    public async Task ThreadSafety_ConcurrentOnSuccessAndOnFailure()
    {
        var cc = new AdaptiveConcurrencyController(10, minLimit: 1, maxLimit: 100);

        var tasks = Enumerable.Range(0, 1000).Select(i =>
            Task.Run(() =>
            {
                if (i % 3 == 0)
                    cc.OnTransientFailure();
                else
                    cc.OnSuccess();
            }));

        await Task.WhenAll(tasks);

        cc.CurrentLimit.Should().BeInRange(1, 100);
    }

    [Fact]
    public void Constructor_ClampsInitialToRange()
    {
        var cc = new AdaptiveConcurrencyController(100, minLimit: 1, maxLimit: 10);
        cc.CurrentLimit.Should().Be(10);

        var cc2 = new AdaptiveConcurrencyController(0, minLimit: 3, maxLimit: 10);
        cc2.CurrentLimit.Should().Be(3);
    }
}
