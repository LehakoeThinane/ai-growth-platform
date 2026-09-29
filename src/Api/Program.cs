using AiGrowthPlatform.Business.Application;
using AiGrowthPlatform.Business.Infrastructure;
using AiGrowthPlatform.Api.Infrastructure;
using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Agents.Workflows;
using AiGrowthPlatform.Api.Agents;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Register application services
builder.Services.AddBusinessApplication();

builder.Services.AddBusinessInfrastructure(builder.Configuration);
builder.Services.AddAgents(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IBusinessCatalog>(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value.EffectiveCatalogSource == "Demo"
    ? new DemoCatalog() : new BusinessCatalog(sp.GetRequiredService<AiGrowthPlatform.Business.Infrastructure.Data.BusinessDbContext>()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddConcurrencyLimiter("agent-runs", limiter => { limiter.PermitLimit = 4; limiter.QueueLimit = 0; });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BusinessExceptionHandler>();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseMiddleware<ApiAccessMiddleware>();
app.UseRateLimiter();
app.MapControllers();
app.MapAgentEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

public partial class Program { }

record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}
