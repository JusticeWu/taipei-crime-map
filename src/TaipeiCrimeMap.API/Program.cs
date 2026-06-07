using TaipeiCrimeMap.API.Middleware;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Infrastructure.Extensions;
using TaipeiCrimeMap.Infrastructure.Persistence;

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
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Run();

// 讓測試專案可以存取 Program 型別
public partial class Program { }