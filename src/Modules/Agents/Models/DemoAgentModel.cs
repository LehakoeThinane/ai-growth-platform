using System.Text.Json;

namespace AiGrowthPlatform.Agents.Models;

// A deterministic demonstration of the workflow. This is explicitly not a model-selected AI run.
public sealed class DemoAgentModel : IAgentModel
{
    public string Mode => "Demo";
    public IAgentSession CreateSession() => new Session();

    private sealed class Session : IAgentSession
    {
        public Task<ToolCall> DecideAsync(AgentRun run, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool Has(string name) => run.Observations.Any(x => x.Tool == name);
            ToolCall Call(string name, object arguments) => new(Guid.NewGuid().ToString("N"), name,
                JsonSerializer.Serialize(arguments, AgentJson.Options));
            ToolCall next;
            if (run.Request.Workflow == "growth")
            {
                if (!Has("get_organization")) next = Call("get_organization", new { });
                else if (!Has("list_products")) next = Call("list_products", new { });
                else if (!Has("search_knowledge")) next = Call("search_knowledge", new { query = "product catalogue" });
                else
                {
                    var products = run.Observations.Last(x => x.Tool == "list_products").Result
                        .Deserialize<ProductInfo[]>(AgentJson.Options)!;
                    var plan = products.Length == 0
                        ? "Add products to the catalogue before developing product-specific growth experiments."
                        : "Draft hypothesis: improve the catalogue for " + string.Join(", ", products.Take(10).Select(x => x.Name)) +
                          ". Fill missing descriptions and prices, clarify each product's target customer, and test one landing-page message. " +
                          "Measure enquiries before and after the experiment. No sales or conversion data is available to estimate impact.";
                    next = run.Request.AllowWrites && !Has("save_growth_plan")
                        ? Call("save_growth_plan", new { plan })
                        : Call("complete_run", new { recommendation = plan });
                }
            }
            else
            {
                if (!Has("get_ticket")) next = Call("get_ticket", new { });
                else if (!Has("search_knowledge")) next = Call("search_knowledge", new { query = "service recovered" });
                else
                {
                    var ticket = run.Observations.Last(x => x.Tool == "get_ticket").Result.Deserialize<SupportTicket>(AgentJson.Options)!;
                    var update = run.Observations.LastOrDefault(x => x.Tool == "update_ticket");
                    if (update is not null && run.Observations.Last(x => x.Tool == "get_ticket").Step < update.Step)
                        next = Call("get_ticket", new { });
                    else if (run.Request.AllowWrites && ticket.Status == "open")
                        next = Call("update_ticket", new
                        {
                            status = ticket.Category == "service_recovered" && ticket.ServiceHealthy ? "resolved" : "escalated",
                            comment = "Reviewed against SUPPORT-001 using the stored service health evidence."
                        });
                    else next = Call("complete_run", new
                    {
                        recommendation = ticket.Category == "service_recovered" && ticket.ServiceHealthy
                            ? "Continue monitoring the service. Reopen through a human operator if the issue returns."
                            : "Have a human support specialist investigate; the approved recovery procedure does not apply."
                    });
                }
            }
            return Task.FromResult(next);
        }
    }
}
