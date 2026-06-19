using System.Threading.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using StackExchange.Redis;
using TaipeiCrimeMap.API.Middleware;
using TaipeiCrimeMap.API.WebSockets;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.Infrastructure.Extensions;
using TaipeiCrimeMap.Infrastructure.Jobs;
using TaipeiCrimeMap.Infrastructure.Metrics;
using TaipeiCrimeMap.Infrastructure.Persistence;
using TaipeiCrimeMap.Infrastructure.Timing;

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("public-api", context =>
    {
        var limit = context.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:PublicApi", 60);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });

    options.AddPolicy("admin-api", context =>
    {
        var limit = context.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:AdminApi", 20);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

// Secondary Redis（選配，用於跨環境訂閱其他 Server 的指標）
var secondaryRedisConnStr = builder.Configuration.GetConnectionString("SecondaryRedis");
if (!string.IsNullOrWhiteSpace(secondaryRedisConnStr))
{
    var connStr = secondaryRedisConnStr;
    builder.Services.AddKeyedSingleton<IConnectionMultiplexer>("SecondaryRedis", (_, _) =>
        ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { connStr },
            AbortOnConnectFail = false,
            ConnectTimeout = 2000,
            SyncTimeout = 2000,
            ConnectRetry = 0,
        }));
}

// Server metrics：背景常駐任務，啟動即持續向 Garnet 發布指標
// 同時以 Singleton 注入，讓 WebSocketHandler 可以呼叫 AddConnection/RemoveConnection
builder.Services.AddSingleton<ServerMetricsService>(sp =>
    new ServerMetricsService(
        sp.GetService<IConnectionMultiplexer>(),
        sp.GetKeyedService<IConnectionMultiplexer>("SecondaryRedis"),
        sp.GetRequiredService<ILogger<ServerMetricsService>>()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<ServerMetricsService>());
builder.Services.AddSingleton<ServerMetricsWebSocketHandler>();

// Domain / Infrastructure services
builder.Services.AddMemoryCache();
builder.Services.AddInfrastructure(builder.Configuration);

// Case import background job system
builder.Services.AddSingleton<ICaseImportJobStore, CaseImportJobStore>();
var maxConcurrency = builder.Configuration.GetValue("CaseImportWorker:MaxConcurrency", 5);
builder.Services.AddSingleton(new AdaptiveConcurrencyController(maxConcurrency, minLimit: 1, maxLimit: Math.Max(maxConcurrency * 2, 20)));
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<CaseImportWorker>();

// Application handlers
builder.Services.AddScoped<ImportCsvCommandHandler>();
builder.Services.AddScoped<GetCrimesByFilterQueryHandler>();
builder.Services.AddScoped<GetHeatmapQueryHandler>();
builder.Services.AddScoped<GeocodeBatchCommandHandler>();
builder.Services.AddScoped<GetCrimeStatsQueryHandler>();
builder.Services.AddScoped<GetCrimeByIdQueryHandler>();
builder.Services.AddScoped<UpdateCoordinateByLocationCommandHandler>();

// OpenTelemetry + Application Insights
// 連線字串不存在時不啟用（graceful degradation），避免本機/測試環境噴錯
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    // UseAzureMonitor 會同時設定 Traces、Metrics、Logs 三層的 exporter
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
}

// Timing
builder.Services.Configure<TimingOptions>(
    builder.Configuration.GetSection(TimingOptions.SectionName));

// Admin Basic Authentication（用於 /api/crime/coordinate）
builder.Services.Configure<AdminAuthOptions>(
    builder.Configuration.GetSection(AdminAuthOptions.SectionName));

var timingEnabled = builder.Configuration
    .GetSection(TimingOptions.SectionName)
    .GetValue<bool>("Enabled");

if (timingEnabled)
{
    builder.Services.AddScoped<ITimingTracker, TimingTracker>();
}
else
{
    builder.Services.AddScoped<ITimingTracker>(_ => NullTimingTracker.Instance);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();

    var migrator = scope.ServiceProvider.GetRequiredService<DbUpMigrator>();
    migrator.MigrateUp();

    // 預熱資料庫連線，避免第一個使用者請求等待冷啟動
    try
    {
        var connStr = scope.ServiceProvider
            .GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection");
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.ExecuteScalar();
        app.Logger.LogInformation("資料庫連線預熱完成");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "資料庫連線預熱失敗，繼續啟動");
    }
}

app.UseExceptionHandler();
app.UseMiddleware<ObservabilityMiddleware>();
app.UseMiddleware<TimingMiddleware>();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/crime/coordinate")
        || context.Request.Path.StartsWithSegments("/api/admin"),
    branch => branch.UseMiddleware<BasicAuthMiddleware>());

app.MapGet("/admin", () => Results.Redirect("/admin.html"));

app.Map("/ws/metrics", async context =>
{
    var handler = context.RequestServices.GetRequiredService<ServerMetricsWebSocketHandler>();
    await handler.HandleAsync(context);
}).RequireRateLimiting("admin-api");

app.MapControllers();
app.Run();

// 讓測試專案可以存取 Program 型別
public partial class Program { }