using System.Text.Json;
using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Agents.Models;
using AiGrowthPlatform.Agents.Persistence;
using AiGrowthPlatform.Agents.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiGrowthPlatform.Agents.Tests;

public sealed class AgentRunnerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "agent-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AgentOptions _options;
    private readonly AgentStore _store;

    public AgentRunnerTests()
    {
        _options = new AgentOptions { DataDirectory = _directory };
        _store = new AgentStore(Options.Create(_options));
    }

    private AgentRunner Runner(IAgentModel? model = null, IBusinessCatalog? catalog = null) => new(
        model ?? new DemoAgentModel(), new WorkflowTools(catalog ?? new DemoCatalog(), _store), _store,
        Options.Create(_options), NullLogger<AgentRunner>.Instance);

    private static StartRunRequest Growth(bool writes = true) => new()
    {
        Workflow = "growth", OrganizationId = DemoCatalog.OrganizationId, Task = "Review my catalogue and draft a growth plan.", AllowWrites = writes
    };

    private async Task<StartRunRequest> Support(string category = "service_recovered", bool healthy = true, bool writes = true)
    {
        await _store.CreateTicketAsync(new("INC-1042", DemoCatalog.OrganizationId, "Test incident", category, healthy, "open"), TestContext.Current.CancellationToken);
        return new() { Workflow = "support", OrganizationId = DemoCatalog.OrganizationId, Task = "Resolve if permitted, otherwise escalate.", TicketId = "INC-1042", AllowWrites = writes };
    }

    [Fact]
    public async Task Growth_saves_plan_and_loads_only_verified_memory_on_next_run()
    {
        var first = await Runner().RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Equal("completed", first.Status);
        Assert.Equal("Demo", first.Mode);
        Assert.Null(first.LoadedMemory);
        Assert.Equal(4, first.Observations.Count);
        var plan = await _store.GetPlanAsync(first.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(plan);
        Assert.Contains("Starter consultation", plan.Content);
        var next = await Runner().RunAsync(Growth(false), TestContext.Current.CancellationToken);
        Assert.Equal(first.Id, next.LoadedMemory?.LastRunId);
        Assert.Null(await _store.GetPlanAsync(next.Id, TestContext.Current.CancellationToken));
        Assert.Equal(first.Outcome, next.LoadedMemory?.LastOutcome);
        var reloaded = new AgentStore(Options.Create(_options));
        Assert.Equal(first.Status, (await reloaded.GetRunAsync(first.Id, TestContext.Current.CancellationToken))?.Status);
        Assert.Equal(next.Id, (await reloaded.GetMemoryAsync(Growth(), TestContext.Current.CancellationToken))?.LastRunId);
    }

    [Theory]
    [InlineData("service_recovered", true, "resolved")]
    [InlineData("service_recovered", false, "escalated")]
    [InlineData("billing", true, "escalated")]
    public async Task Support_resolves_only_approved_recoveries_and_verifies_updates(string category, bool healthy, string expected)
    {
        var request = await Support(category, healthy);
        var run = await Runner().RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("completed", run.Status);
        Assert.Contains(expected, run.Outcome!);
        Assert.Equal("get_ticket", run.Observations.Last().Tool);
        Assert.Equal(expected, (await _store.GetTicketAsync(request.OrganizationId, request.TicketId!, TestContext.Current.CancellationToken))?.Status);
        Assert.Null(run.LoadedMemory);
    }

    [Fact]
    public async Task Read_only_support_never_updates_ticket()
    {
        var request = await Support(writes: false);
        var run = await Runner().RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("completed", run.Status);
        Assert.DoesNotContain(run.Observations, x => x.Tool == "update_ticket");
        Assert.Equal("open", (await _store.GetTicketAsync(request.OrganizationId, request.TicketId!, TestContext.Current.CancellationToken))?.Status);
    }

    [Fact]
    public async Task Cross_workflow_tool_is_rejected_before_execution()
    {
        var run = await Runner(new ScriptedModel(Call("update_ticket", new { status = "resolved", comment = "test" }))).RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Equal("rejected", run.Status);
        Assert.Empty(run.Observations);
        Assert.Contains(run.Events, x => x.Type == "execution_error");
    }

    [Fact]
    public async Task Write_permission_is_enforced_even_when_model_requests_write()
    {
        var run = await Runner(new ScriptedModel(Call("save_growth_plan", new { plan = "An unauthorized plan" }))).RunAsync(Growth(false), TestContext.Current.CancellationToken);
        Assert.Equal("rejected", run.Status);
        Assert.Contains("not authorized", run.Outcome!);
        Assert.Null(await _store.GetPlanAsync(run.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Model_cannot_claim_completion_before_reading_evidence()
    {
        var run = await Runner(new ScriptedModel(Call("complete_run", new { recommendation = "Everything is fixed" }))).RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Equal("rejected", run.Status);
        Assert.Null(run.Recommendation);
        Assert.Null(await _store.GetMemoryAsync(Growth(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{\"query\":12}")]
    [InlineData("{\"query\":\"product\",\"organizationId\":\"other\"}")]
    [InlineData("{\"query\":\"product\",\"query\":\"other\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    public async Task Invalid_tool_arguments_stop_the_run(string json)
    {
        var run = await Runner(new ScriptedModel(new ToolCall("call", "search_knowledge", json))).RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Equal("rejected", run.Status);
        Assert.Empty(run.Observations);
    }

    [Fact]
    public async Task Endless_model_is_bounded_and_audited()
    {
        _options.MaxSteps = 2;
        var run = await Runner(new ScriptedModel(Call("get_organization", new { }))).RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Equal("step_limit", run.Status);
        Assert.Equal(2, run.Observations.Count);
        Assert.NotNull(run.FinishedAt);
        Assert.Equal("run_stopped", (await _store.GetRunAsync(run.Id, TestContext.Current.CancellationToken))!.Events.Last().Type);
    }

    [Fact]
    public async Task Tool_failure_cannot_produce_success_or_persist_memory()
    {
        var run = await Runner(catalog: new FailingCatalog()).RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Equal("failed", run.Status);
        Assert.Empty(run.Observations);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(run));
        Assert.Null(await _store.GetMemoryAsync(Growth(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancelled_run_is_saved_with_terminal_status()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var run = await Runner().RunAsync(Growth(), cancellation.Token);
        Assert.Equal("cancelled", run.Status);
        Assert.NotNull((await _store.GetRunAsync(run.Id, TestContext.Current.CancellationToken))?.FinishedAt);
    }

    [Fact]
    public async Task Memory_is_scoped_to_workflow_organization_and_ticket()
    {
        await Runner().RunAsync(Growth(), TestContext.Current.CancellationToken);
        Assert.Null(await _store.GetMemoryAsync(Growth() with { OrganizationId = Guid.NewGuid() }, TestContext.Current.CancellationToken));
        var support = await Support();
        var supportRun = await Runner().RunAsync(support, TestContext.Current.CancellationToken);
        Assert.Null(supportRun.LoadedMemory);
        Assert.Null(await _store.GetMemoryAsync(support with { TicketId = "INC-999" }, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../run-secret")]
    [InlineData("INC-../../secret")]
    [InlineData(null)]
    public async Task Invalid_ticket_identifiers_are_rejected_before_storage(string? id)
    {
        var request = Growth() with { Workflow = "support", TicketId = id };
        await Assert.ThrowsAsync<AgentValidationException>(() => Runner().RunAsync(request, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Store_rejects_stale_updates_and_cross_organization_reads()
    {
        var request = await Support();
        await _store.UpdateTicketAsync(request.OrganizationId, request.TicketId!, 1, "resolved", "verified", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AgentValidationException>(() => _store.UpdateTicketAsync(request.OrganizationId, request.TicketId!, 1, "escalated", "stale", TestContext.Current.CancellationToken));
        Assert.Null(await _store.GetTicketAsync(Guid.NewGuid(), request.TicketId!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Resolution_policy_is_enforced_by_store()
    {
        var request = await Support("billing", false);
        await Assert.ThrowsAsync<AgentValidationException>(() => _store.UpdateTicketAsync(request.OrganizationId, request.TicketId!, 1, "resolved", "pretend", TestContext.Current.CancellationToken));
        Assert.Equal("open", (await _store.GetTicketAsync(request.OrganizationId, request.TicketId!, TestContext.Current.CancellationToken))?.Status);
    }

    [Fact]
    public async Task Completion_requires_read_after_write()
    {
        var request = await Support();
        var model = new ScriptedModel(Call("get_ticket", new { }), Call("search_knowledge", new { query = "service" }),
            Call("update_ticket", new { status = "resolved", comment = "Verified" }), Call("complete_run", new { recommendation = "Monitor" }));
        var run = await Runner(model).RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal("rejected", run.Status);
        Assert.Contains("Re-read", run.Outcome!);
        Assert.Contains(run.Observations, x => x.Tool == "update_ticket");
    }

    [Fact]
    public async Task Restart_marks_unfinished_runs_interrupted()
    {
        var run = new AgentRun { Request = Growth(), Mode = "Demo" };
        await _store.SaveRunAsync(run, TestContext.Current.CancellationToken);
        await _store.MarkInterruptedRunsAsync(TestContext.Current.CancellationToken);
        Assert.Equal("interrupted", (await _store.GetRunAsync(run.Id, TestContext.Current.CancellationToken))?.Status);
    }

    private static ToolCall Call(string name, object args) => new(Guid.NewGuid().ToString(), name, JsonSerializer.Serialize(args));

    private sealed class ScriptedModel(params ToolCall[] calls) : IAgentModel, IAgentSession
    {
        private int _index;
        public string Mode => "Test";
        public IAgentSession CreateSession() => this;
        public Task<ToolCall> DecideAsync(AgentRun run, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken) =>
            Task.FromResult(calls[Math.Min(_index++, calls.Length - 1)]);
    }

    private sealed class FailingCatalog : IBusinessCatalog
    {
        public Task<OrganizationInfo?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken) => throw new IOException("secret database details");
        public Task<IReadOnlyList<ProductInfo>> GetProductsAsync(Guid organizationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    public void Dispose() => Directory.Delete(_directory, true);
}

