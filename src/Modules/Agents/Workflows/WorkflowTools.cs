using System.Text.Json;
using AiGrowthPlatform.Agents.Persistence;

namespace AiGrowthPlatform.Agents.Workflows;

public sealed class WorkflowTools(IBusinessCatalog catalog, AgentStore store)
{
    public static IReadOnlyList<ToolDefinition> Definitions(string workflow) => workflow == "growth" ?
    [
        new("get_organization", "Read the organization selected for this run.", []),
        new("list_products", "Read up to 100 products for the selected organization.", []),
        new("search_knowledge", "Search approved growth guidance. Use product catalogue as a query.", new() { ["query"] = "Search terms" }),
        new("save_growth_plan", "Save a draft plan after reading the organization, products, and guidance. Requires write permission.", new() { ["plan"] = "Evidence-based draft plan, at most 6000 characters" }),
        CompleteDefinition
    ] :
    [
        new("get_ticket", "Read the selected ticket. Also use after updating to verify the saved status.", []),
        new("search_knowledge", "Search approved support procedures. Use service recovered as a query.", new() { ["query"] = "Search terms" }),
        new("update_ticket", "Update an open ticket after reading it and the approved procedure. Requires write permission. Resolve only a service_recovered ticket with serviceHealthy=true; otherwise escalate.", new() { ["status"] = "resolved or escalated", ["comment"] = "A short factual action note" }),
        CompleteDefinition
    ];

    private static ToolDefinition CompleteDefinition => new("complete_run",
        "Finish after required reads and any permitted write. Actions and evidence are reported by the server. Provide recommendations only, never assert an action succeeded.",
        new() { ["recommendation"] = "Recommendations or next steps, at most 6000 characters" });

    public static Dictionary<string, string> Validate(ToolCall call, IReadOnlyList<ToolDefinition> definitions)
    {
        var definition = definitions.SingleOrDefault(x => x.Name == call.Name)
            ?? throw new AgentValidationException("The requested tool is not allowed for this workflow.");
        if (call.Arguments.Length > 16000) throw new AgentValidationException("Tool arguments are too large.");
        using var document = JsonDocument.Parse(call.Arguments);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new AgentValidationException("Tool arguments must be an object.");
        var fields = new Dictionary<string, string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!definition.Fields.ContainsKey(property.Name) || property.Value.ValueKind != JsonValueKind.String ||
                !fields.TryAdd(property.Name, property.Value.GetString()!))
                throw new AgentValidationException("Unexpected, duplicated, or incorrectly typed tool argument.");
            var max = property.Name is "plan" or "recommendation" ? 6000 : 1000;
            if (string.IsNullOrWhiteSpace(fields[property.Name]) || fields[property.Name].Length > max)
                throw new AgentValidationException("Tool argument is empty or exceeds its limit.");
        }
        if (definition.Fields.Keys.Any(x => !fields.ContainsKey(x)))
            throw new AgentValidationException("Required tool argument is missing.");
        return fields;
    }

    private static ToolObservation Require(AgentRun run, string name) => run.Observations.LastOrDefault(x => x.Tool == name)
        ?? throw new AgentValidationException($"The {name} tool must succeed first.");

    private static void RequireKnowledge(AgentRun run)
    {
        var result = Require(run, "search_knowledge").Result;
        if (result.GetArrayLength() == 0) throw new AgentValidationException("No approved knowledge was retrieved.");
    }

    public async Task<JsonElement> ExecuteAsync(AgentRun run, string name, Dictionary<string, string> args, CancellationToken ct)
    {
        var request = run.Request;
        switch (name)
        {
            case "get_organization":
                return AgentJson.Element(await catalog.GetOrganizationAsync(request.OrganizationId, ct)
                    ?? throw new AgentValidationException("Organization not found."));
            case "list_products":
                Require(run, "get_organization");
                return AgentJson.Element(await catalog.GetProductsAsync(request.OrganizationId, ct));
            case "search_knowledge":
                return AgentJson.Element(KnowledgeCatalog.Search(request.Workflow, args["query"]));
            case "save_growth_plan":
                RequireWrites(request);
                Require(run, "get_organization");
                Require(run, "list_products");
                RequireKnowledge(run);
                return AgentJson.Element(await store.SavePlanAsync(run, args["plan"], ct));
            case "get_ticket":
                return AgentJson.Element(await store.GetTicketAsync(request.OrganizationId, request.TicketId!, ct)
                    ?? throw new AgentValidationException("Ticket not found in this organization."));
            case "update_ticket":
                RequireWrites(request);
                RequireKnowledge(run);
                var ticket = Require(run, "get_ticket").Result.Deserialize<SupportTicket>(AgentJson.Options)!;
                return AgentJson.Element(await store.UpdateTicketAsync(request.OrganizationId, request.TicketId!,
                    ticket.Version, args["status"], args["comment"], ct));
            default:
                throw new AgentValidationException("Unknown tool.");
        }
    }

    private static void RequireWrites(StartRunRequest request)
    {
        if (!request.AllowWrites) throw new AgentValidationException("Writes were not authorized for this run.");
    }

    public async Task<string> CompleteAsync(AgentRun run, CancellationToken ct)
    {
        RequireKnowledge(run);
        if (run.Request.Workflow == "growth")
        {
            Require(run, "get_organization");
            Require(run, "list_products");
            if (!run.Request.AllowWrites) return "Catalogue and growth guidance reviewed. No plan was saved because this run is read-only.";
            Require(run, "save_growth_plan");
            var plan = await store.GetPlanAsync(run.Id, ct)
                ?? throw new AgentValidationException("The growth plan could not be verified in storage.");
            return $"Draft growth plan {plan.Id} saved and verified. No campaign was published.";
        }
        var read = Require(run, "get_ticket");
        var ticket = read.Result.Deserialize<SupportTicket>(AgentJson.Options)!;
        var update = run.Observations.LastOrDefault(x => x.Tool == "update_ticket");
        if (update is not null && read.Step <= update.Step)
            throw new AgentValidationException("Re-read the ticket after the update before completing.");
        if (run.Request.AllowWrites && ticket.Status == "open")
            throw new AgentValidationException("An open ticket must be resolved or escalated before completing a writable run.");
        var saved = await store.GetTicketAsync(run.Request.OrganizationId, run.Request.TicketId!, ct);
        if (saved is null || saved.Version != ticket.Version)
            throw new AgentValidationException("Ticket changed after verification. Re-read it in a new run.");
        return update is null
            ? $"Ticket {ticket.Id} reviewed. Current status: {ticket.Status}. No ticket update was performed."
            : $"Ticket {ticket.Id} updated and verified as {ticket.Status}.";
    }
}
