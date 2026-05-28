using TaipeiCrimeMap.API.Middleware;
using TaipeiCrimeMap.Application.Handlers;
using TaipeiCrimeMap.Domain.Repositories;
using TaipeiCrimeMap.Domain.Services;
using TaipeiCrimeMap.Infrastructure.Csv;
using TaipeiCrimeMap.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Domain / Infrastructure services
builder.Services.AddSingleton<ICrimeRepository, InMemoryCrimeRepository>();
builder.Services.AddSingleton<ICsvParser, CsvParser>();

// Application handlers
builder.Services.AddScoped<ImportCsvCommandHandler>();
builder.Services.AddScoped<GetCrimesByFilterQueryHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();

// 讓測試專案可以存取 Program 型別
public partial class Program { }