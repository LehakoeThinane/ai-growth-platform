using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiGrowthPlatform.Agents.Persistence;

// Local, single-process persistence. Replace this adapter with database transactions before scaling out.
public sealed class AgentStore
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AgentStore(IOptions<AgentOptions> options)
    {
        _directory = Path.GetFullPath(options.Value.DataDirectory);
        Directory.CreateDirectory(_directory);
    }

    private string PathFor(string key) => Path.Combine(_directory, key + ".json");

    private async Task<T?> ReadAsync<T>(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, AgentJson.Options, ct);
    }

    private async Task WriteAsync<T>(string key, T value)
    {
        var path = PathFor(key);
        var temporary = path + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, value, AgentJson.Options);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private async Task<T> Locked<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return await action(); }
        finally { _gate.Release(); }
    }

    public Task SaveRunAsync(AgentRun run, CancellationToken ct = default) => Locked(async () =>
    {
        await WriteAsync($"run-{run.Id:N}", run);
        return true;
    }, ct);

    public Task<AgentRun?> GetRunAsync(Guid id, CancellationToken ct) => Locked(async () =>
    {
        var run = await ReadAsync<AgentRun>($"run-{id:N}", ct);
        // No worker resumes runs after a process restart. Never present an interrupted run as successful.
        return run;
    }, ct);

    public async Task MarkInterruptedRunsAsync(CancellationToken ct)
    {
        await Locked(async () =>
        {
            foreach (var path in Directory.EnumerateFiles(_directory, "run-*.json"))
            {
                var key = Path.GetFileNameWithoutExtension(path);
                var run = await ReadAsync<AgentRun>(key, ct);
                if (run?.Status != "running") continue;
                run.Status = "interrupted";
                run.FinishedAt = DateTimeOffset.UtcNow;
                run.Outcome = "The process stopped during execution. Inspect action records before starting another run.";
                run.Events.Add(new(run.Events.Count + 1, 0, DateTimeOffset.UtcNow, "interrupted", AgentJson.Element(new { run.Outcome })));
                await WriteAsync(key, run);
            }
            return true;
        }, ct);
    }

    private static string MemoryKey(StartRunRequest request) =>
        $"memory-{request.OrganizationId:N}-{request.Workflow}-{request.TicketId ?? "none"}";

    public Task<RunMemory?> GetMemoryAsync(StartRunRequest request, CancellationToken ct) =>
        Locked(() => ReadAsync<RunMemory>(MemoryKey(request), ct), ct);

    public Task SaveMemoryAsync(AgentRun run, CancellationToken ct) => Locked(async () =>
    {
        await WriteAsync(MemoryKey(run.Request), new RunMemory(run.Request.Workflow,
            run.Request.OrganizationId, run.Request.TicketId, run.Id, run.Outcome!, DateTimeOffset.UtcNow));
        return true;
    }, ct);

    public Task<GrowthPlan> SavePlanAsync(AgentRun run, string content, CancellationToken ct) => Locked(async () =>
    {
        var key = $"plan-{run.Id:N}";
        var existing = await ReadAsync<GrowthPlan>(key, ct);
        if (existing is not null) return existing;
        var plan = new GrowthPlan(Guid.NewGuid(), run.Request.OrganizationId, run.Id, content, DateTimeOffset.UtcNow);
        await WriteAsync(key, plan);
        return plan;
    }, ct);

    public Task<GrowthPlan?> GetPlanAsync(Guid runId, CancellationToken ct) =>
        Locked(() => ReadAsync<GrowthPlan>($"plan-{runId:N}", ct), ct);

    public Task<SupportTicket?> GetTicketAsync(Guid organizationId, string id, CancellationToken ct) =>
        Locked(() => ReadAsync<SupportTicket>($"ticket-{organizationId:N}-{id}", ct), ct);

    public Task CreateTicketAsync(SupportTicket ticket, CancellationToken ct) => Locked(async () =>
    {
        var key = $"ticket-{ticket.OrganizationId:N}-{ticket.Id}";
        if (File.Exists(PathFor(key))) throw new AgentValidationException("That ticket already exists.");
        await WriteAsync(key, ticket);
        return true;
    }, ct);

    public Task<SupportTicket> UpdateTicketAsync(Guid organizationId, string id, int expectedVersion,
        string status, string comment, CancellationToken ct) => Locked(async () =>
    {
        var key = $"ticket-{organizationId:N}-{id}";
        var ticket = await ReadAsync<SupportTicket>(key, ct)
            ?? throw new AgentValidationException("Ticket not found in this organization.");
        if (ticket.Version != expectedVersion)
            throw new AgentValidationException("Ticket changed during this run. Start a new run to re-evaluate it.");
        if (ticket.Status != "open") throw new AgentValidationException("Only open tickets may be updated.");
        if (status == "resolved" && (ticket.Category != "service_recovered" || !ticket.ServiceHealthy))
            throw new AgentValidationException("The approved resolution procedure does not apply. Escalate this ticket.");
        if (status is not ("resolved" or "escalated")) throw new AgentValidationException("Unsupported ticket status.");
        var updated = ticket with { Status = status, Comment = comment, Version = ticket.Version + 1 };
        await WriteAsync(key, updated);
        return updated;
    }, ct);
}
