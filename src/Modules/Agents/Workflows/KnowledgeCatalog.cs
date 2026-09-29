namespace AiGrowthPlatform.Agents.Workflows;

public static class KnowledgeCatalog
{
    public static readonly IReadOnlyList<KnowledgeArticle> Articles =
    [
        new("GROWTH-001", "growth", "Product catalogue review",
            "Use the organization's actual products. Identify missing descriptions or prices, propose clearer positioning and a small measurable marketing experiment. Label ideas as hypotheses. Never invent sales, conversion rates, competitors, or guaranteed growth. Save a draft only; do not publish campaigns."),
        new("SUPPORT-001", "support", "Service recovered resolution",
            "An open ticket may be marked resolved only when its category is service_recovered and the trusted ticket record says serviceHealthy=true. This records a verified recovery; it does not repair a service. For all other open incidents, escalate to a human. Re-read the ticket after updating to verify its status.")
    ];

    public static IReadOnlyList<KnowledgeArticle> Search(string workflow, string query) => Articles
        .Where(x => x.Workflow == workflow)
        .Where(x => query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(term => (x.Title + " " + x.Content).Contains(term, StringComparison.OrdinalIgnoreCase)))
        .ToArray();
}
