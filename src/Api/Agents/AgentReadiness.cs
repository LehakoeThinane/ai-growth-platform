using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Business.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiGrowthPlatform.Api.Agents;

public static class AgentReadiness
{
    public static async Task<IResult> CheckAsync(IOptions<AgentOptions> options, BusinessDbContext db, CancellationToken ct)
    {
        var source = options.Value.EffectiveCatalogSource;
        var database = "not_required";
        var ready = true;
        if (source == "Database")
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var pending = await db.Database.GetPendingMigrationsAsync(timeout.Token);
                if (pending.Any()) { database = "migrations_pending"; ready = false; }
                else
                {
                    await db.Organizations.AsNoTracking().AnyAsync(timeout.Token);
                    await db.Products.AsNoTracking().AnyAsync(timeout.Token);
                    database = "ready";
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                database = "unavailable";
                ready = false;
            }
        }
        return Results.Json(new
        {
            ready, mode = options.Value.Mode, catalogSource = source, database,
            model = options.Value.Mode == "Demo" ? "demo_no_model_calls" : "configured_not_contacted",
            note = "This checks catalogue readiness. Model credentials and model access are verified only by a live run."
        }, statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }
}
