using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Agents.Persistence;
using AiGrowthPlatform.Agents.Workflows;
using Microsoft.Extensions.Options;

namespace AiGrowthPlatform.Api.Agents;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agent").AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (AgentValidationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapGet("/workflows", (IOptions<AgentOptions> options) => Results.Ok(new
        {
            mode = options.Value.Mode,
            catalogSource = options.Value.EffectiveCatalogSource,
            demoOrganizationId = options.Value.EffectiveCatalogSource == "Demo" ? DemoCatalog.OrganizationId : (Guid?)null,
            workflows = new[] { new { id = "growth", tools = WorkflowTools.Definitions("growth") },
                new { id = "support", tools = WorkflowTools.Definitions("support") } }
        }));
        group.MapGet("/readiness", AgentReadiness.CheckAsync);

        group.MapPost("/runs", async (StartRunRequest request, AgentRunner runner, CancellationToken ct) =>
        {
            var run = await runner.RunAsync(request, ct);
            return Results.Created($"/api/agent/runs/{run.Id}", run);
        }).RequireRateLimiting("agent-runs");

        group.MapGet("/runs/{id:guid}", async (Guid id, AgentStore store, CancellationToken ct) =>
            await store.GetRunAsync(id, ct) is { } run ? Results.Ok(run) : Results.NotFound());
        group.MapGet("/runs/{id:guid}/events", async (Guid id, AgentStore store, CancellationToken ct) =>
            await store.GetRunAsync(id, ct) is { } run ? Results.Ok(run.Events) : Results.NotFound());
        group.MapGet("/runs/{id:guid}/plan", async (Guid id, AgentStore store, CancellationToken ct) =>
            await store.GetPlanAsync(id, ct) is { } plan ? Results.Ok(plan) : Results.NotFound());

        group.MapPost("/tickets", async (CreateTicket request, IBusinessCatalog catalog, AgentStore store, CancellationToken ct) =>
        {
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true) ||
                string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Category))
                return Results.BadRequest(new { error = "Provide an INC-number ID, organizationId, title, and category within their limits." });
            if (await catalog.GetOrganizationAsync(request.OrganizationId, ct) is null) return Results.NotFound(new { error = "Organization not found." });
            var ticket = new SupportTicket(request.Id, request.OrganizationId, request.Title.Trim(), request.Category.Trim(), request.ServiceHealthy, "open");
            await store.CreateTicketAsync(ticket, ct);
            return Results.Created($"/api/agent/tickets/{ticket.OrganizationId}/{ticket.Id}", ticket);
        });

        group.MapGet("/tickets/{organizationId:guid}/{id}", async (Guid organizationId, string id, AgentStore store, CancellationToken ct) =>
        {
            if (!Regex.IsMatch(id, "^INC-[0-9]{1,12}$", RegexOptions.CultureInvariant)) return Results.BadRequest(new { error = "Invalid ticket ID." });
            return await store.GetTicketAsync(organizationId, id, ct) is { } ticket ? Results.Ok(ticket) : Results.NotFound();
        });
    }

    public sealed record CreateTicket(
        [property: Required, RegularExpression("^INC-[0-9]{1,12}$")] string Id,
        Guid OrganizationId,
        [property: Required, StringLength(200)] string Title,
        [property: Required, StringLength(80)] string Category,
        bool ServiceHealthy);
}
