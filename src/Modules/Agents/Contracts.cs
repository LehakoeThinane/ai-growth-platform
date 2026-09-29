using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AiGrowthPlatform.Agents;

public sealed record StartRunRequest
{
    [Required, RegularExpression("^(growth|support)$")]
    public string Workflow { get; init; } = "";
    public Guid OrganizationId { get; init; }
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Task { get; init; } = "";
    [StringLength(40)]
    public string? TicketId { get; init; }
    public bool AllowWrites { get; init; }
}

public sealed class AgentOptions
{
    public string Mode { get; set; } = "Demo";
    public string CatalogSource { get; set; } = "Auto";
    public string EffectiveCatalogSource => CatalogSource == "Auto" ? (Mode == "Demo" ? "Demo" : "Database") : CatalogSource;
    public string DataDirectory { get; set; } = "App_Data/agents";
    public int MaxSteps { get; set; } = 12;
    public int TimeoutSeconds { get; set; } = 120;
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string AccessKey { get; set; } = "";
}

public sealed class AgentRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required StartRunRequest Request { get; init; }
    public required string Mode { get; init; }
    public string CatalogSource { get; init; } = "Demo";
    public string Status { get; set; } = "running";
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Outcome { get; set; }
    public string? Recommendation { get; set; }
    public List<AuditEvent> Events { get; init; } = [];
    public List<ToolObservation> Observations { get; init; } = [];
    public RunMemory? LoadedMemory { get; set; }
}

public sealed record AuditEvent(int Sequence, int Step, DateTimeOffset Timestamp, string Type, JsonElement Data);
public sealed record ToolObservation(int Step, string Tool, JsonElement Result);
public sealed record ToolCall(string Id, string Name, string Arguments);
public sealed record ToolDefinition(string Name, string Description, Dictionary<string, string> Fields)
{
    public object Schema => new
    {
        type = "object",
        properties = Fields.ToDictionary(x => x.Key, x => new { type = "string", description = x.Value }),
        required = Fields.Keys.ToArray(),
        additionalProperties = false
    };
}

public sealed record OrganizationInfo(Guid Id, string Name, string? WebsiteUrl);
public sealed record ProductInfo(Guid Id, string Name, string? Description, decimal? Price);
public sealed record GrowthPlan(Guid Id, Guid OrganizationId, Guid RunId, string Content, DateTimeOffset CreatedAt);
public sealed record SupportTicket(string Id, Guid OrganizationId, string Title, string Category,
    bool ServiceHealthy, string Status, string? Comment = null, int Version = 1);
public sealed record KnowledgeArticle(string Id, string Workflow, string Title, string Content);
public sealed record RunMemory(string Workflow, Guid OrganizationId, string? TicketId, Guid LastRunId,
    string LastOutcome, DateTimeOffset UpdatedAt);

public interface IBusinessCatalog
{
    Task<OrganizationInfo?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductInfo>> GetProductsAsync(Guid organizationId, CancellationToken cancellationToken);
}

public interface IAgentModel
{
    string Mode { get; }
    IAgentSession CreateSession();
}

public interface IAgentSession
{
    Task<ToolCall> DecideAsync(AgentRun run, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken);
}

public sealed class AgentValidationException(string message) : Exception(message);
public sealed class AgentProviderException(string message) : Exception(message);

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}
