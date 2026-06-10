using TaipeiCrimeMap.API.Middleware;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Application.Interfaces;
using TaipeiCrimeMap.Application.Options;
using TaipeiCrimeMap.Infrastructure.Extensions;
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

// Domain / Infrastructure services
builder.Services.AddMemoryCache();
builder.Services.AddInfrastructure(builder.Configuration);

// Application handlers
builder.Services.AddScoped<ImportCsvCommandHandler>();
builder.Services.AddScoped<GetCrimesByFilterQueryHandler>();
builder.Services.AddScoped<GetHeatmapQueryHandler>();
builder.Services.AddScoped<GeocodeBatchCommandHandler>();

// Timing
builder.Services.Configure<TimingOptions>(
    builder.Configuration.GetSection(TimingOptions.SectionName));

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
app.UseMiddleware<TimingMiddleware>();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Run();

// 讓測試專案可以存取 Program 型別
public partial class Program { }