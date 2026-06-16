using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.API.WebSockets;
using TaipeiCrimeMap.Infrastructure.Metrics;
using FluentAssertions;

namespace TaipeiCrimeMap.Integration.Tests.WebSockets;

public class ServerMetricsHandlerTests
{
    private static ServerMetricsWebSocketHandler BuildHandler() =>
        new(
            new ServerMetricsService(),
            Options.Create(new AdminAuthOptions { Username = "admin", Password = "pass" }),
            NullLogger<ServerMetricsWebSocketHandler>.Instance);

    [Fact]
    public async Task HandleAsync_WithoutToken_Returns401WithoutThrowing()
    {
        var handler = BuildHandler();
        var context = new DefaultHttpContext();

        var act = async () => await handler.HandleAsync(context);

        await act.Should().NotThrowAsync();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}
