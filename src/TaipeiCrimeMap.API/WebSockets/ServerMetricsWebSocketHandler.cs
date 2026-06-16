using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.Infrastructure.Metrics;

namespace TaipeiCrimeMap.API.WebSockets;

public sealed class ServerMetricsWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ServerMetricsService _metricsService;
    private readonly AdminAuthOptions _authOptions;
    private readonly IConnectionMultiplexer _primaryRedis;
    private readonly IConnectionMultiplexer? _secondaryRedis;
    private volatile int _connectionCount;

    public ServerMetricsWebSocketHandler(
        ServerMetricsService metricsService,
        IOptions<AdminAuthOptions> authOptions,
        IConnectionMultiplexer primaryRedis,
        IServiceProvider serviceProvider)
    {
        _metricsService = metricsService;
        _authOptions = authOptions.Value;
        _primaryRedis = primaryRedis;
        _secondaryRedis = serviceProvider.GetKeyedService<IConnectionMultiplexer>("SecondaryRedis");
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!IsAuthenticated(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        Interlocked.Increment(ref _connectionCount);

        // 所有傳送都走這個 channel，確保 WebSocket.SendAsync 是單執行緒存取
        var sendChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true,
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        // 啟動三條資料流（全部寫入同一個 channel）
        var producerTask = RunProducerAsync(sendChannel.Writer, cts.Token);
        var ownSubTask = SubscribeToGarnetAsync(_primaryRedis, sendChannel.Writer, cts.Token);
        var secSubTask = _secondaryRedis is not null
            ? SubscribeToGarnetAsync(_secondaryRedis, sendChannel.Writer, cts.Token)
            : Task.CompletedTask;

        try
        {
            // 單一消費者：從 channel 讀取並推送到 WebSocket
            await foreach (var bytes in sendChannel.Reader.ReadAllAsync(cts.Token))
            {
                if (ws.State != WebSocketState.Open) break;
                await ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            cts.Cancel();
            Interlocked.Decrement(ref _connectionCount);
            // 讓背景任務自然結束（已透過 cts 取消）
            await Task.WhenAll(producerTask, ownSubTask, secSubTask).ConfigureAwait(false);
        }
    }

    // 每秒收集自機指標 → 發布到 Garnet → 直接寫入 channel（fallback：Garnet 不可用時仍能顯示）
    private async Task RunProducerAsync(ChannelWriter<byte[]> writer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var metrics = _metricsService.GetMetrics(_connectionCount);
                await _metricsService.PublishAsync(metrics, ct);

                // 直接塞入 channel 作為 fallback（訂閱端也會收到同樣資料，前端去重）
                var json = JsonSerializer.Serialize(metrics, JsonOptions);
                writer.TryWrite(Encoding.UTF8.GetBytes(json));

                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    // 訂閱指定 Garnet 的 metrics:* channel，收到就轉寫入 channel
    private static async Task SubscribeToGarnetAsync(
        IConnectionMultiplexer redis,
        ChannelWriter<byte[]> writer,
        CancellationToken ct)
    {
        ISubscriber? sub = null;
        try
        {
            sub = redis.GetSubscriber();
            await sub.SubscribeAsync(
                RedisChannel.Pattern("metrics:*"),
                (_, message) =>
                {
                    if (!message.IsNullOrEmpty)
                        writer.TryWrite(Encoding.UTF8.GetBytes((string)message!));
                });

            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }
        catch { /* Garnet 不可用時靜默跳過 */ }
        finally
        {
            if (sub is not null)
            {
                try { await sub.UnsubscribeAsync(RedisChannel.Pattern("metrics:*")); }
                catch { }
            }
        }
    }

    private bool IsAuthenticated(HttpContext context)
    {
        var token = context.Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(token))
            return ValidateBase64(token);

        var header = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(header)
            && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return ValidateBase64(header["Basic ".Length..].Trim());

        return false;
    }

    private bool ValidateBase64(string base64)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var idx = decoded.IndexOf(':');
            if (idx < 0) return false;

            var username = decoded[..idx];
            var password = decoded[(idx + 1)..];

            return !string.IsNullOrEmpty(_authOptions.Username)
                && username == _authOptions.Username
                && password == _authOptions.Password;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
