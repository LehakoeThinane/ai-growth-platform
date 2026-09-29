using System.Net;
using System.Text;
using System.Text.Json;
using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Agents.Models;
using AiGrowthPlatform.Agents.Workflows;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiGrowthPlatform.Agents.Tests;

public sealed class OpenAiAgentModelTests
{
    [Fact]
    public async Task Adapter_replays_reasoning_tool_call_and_result_without_provider_storage()
    {
        var bodies = new List<JsonElement>();
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            bodies.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone());
            return Json(new
            {
                status = "completed",
                output = new object[]
                {
                    new { type = "reasoning", id = "reason-1", summary = Array.Empty<object>(), encrypted_content = "encrypted-context" },
                    new { type = "function_call", call_id = "call-1", name = "get_organization", arguments = "{}" }
                }
            });
        }));
        var model = new OpenAiAgentModel(client, Options.Create(new AgentOptions { ApiKey = "test-key", Model = "configured-model" }));
        var session = model.CreateSession();
        var run = new AgentRun { Request = new() { Workflow = "growth", OrganizationId = DemoCatalog.OrganizationId, Task = "Review" }, Mode = "OpenAI" };
        var tools = WorkflowTools.Definitions("growth");
        var call = await session.DecideAsync(run, tools, TestContext.Current.CancellationToken);
        Assert.Equal("get_organization", call.Name);
        run.Observations.Add(new(1, call.Name, AgentJson.Element(new { name = "Test organization" })));
        await session.DecideAsync(run, tools, TestContext.Current.CancellationToken);
        Assert.False(bodies[0].GetProperty("store").GetBoolean());
        Assert.False(bodies[0].GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Equal("configured-model", bodies[0].GetProperty("model").GetString());
        var input = bodies[1].GetProperty("input").EnumerateArray().ToArray();
        Assert.Equal("reasoning", input[1].GetProperty("type").GetString());
        Assert.Equal("function_call", input[2].GetProperty("type").GetString());
        Assert.Equal("function_call_output", input[3].GetProperty("type").GetString());
        Assert.Equal("call-1", input[3].GetProperty("call_id").GetString());
        Assert.Contains("Test organization", input[3].GetProperty("output").GetString());
    }

    [Theory]
    [InlineData("incomplete", 1)]
    [InlineData("completed", 0)]
    [InlineData("completed", 2)]
    public async Task Adapter_rejects_incomplete_or_ambiguous_model_decisions(string status, int callCount)
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(Json(new
        {
            status,
            output = Enumerable.Range(0, callCount).Select(i => new { type = "function_call", call_id = $"call-{i}", name = "get_organization", arguments = "{}" })
        }))));
        var model = new OpenAiAgentModel(client, Options.Create(new AgentOptions { ApiKey = "test", Model = "test" }));
        var run = new AgentRun { Request = new() { Workflow = "growth", Task = "Review" }, Mode = "OpenAI" };
        await Assert.ThrowsAsync<AgentProviderException>(() => model.CreateSession().DecideAsync(run, WorkflowTools.Definitions("growth"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Provider_errors_do_not_expose_response_body()
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            { Content = new StringContent("private provider diagnostic") })));
        var model = new OpenAiAgentModel(client, Options.Create(new AgentOptions { ApiKey = "test", Model = "test" }));
        var run = new AgentRun { Request = new() { Workflow = "growth", Task = "Review" }, Mode = "OpenAI" };
        var exception = await Assert.ThrowsAsync<AgentProviderException>(() => model.CreateSession().DecideAsync(run, WorkflowTools.Definitions("growth"), TestContext.Current.CancellationToken));
        Assert.Contains("401", exception.Message);
        Assert.DoesNotContain("private", exception.Message);
    }

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }
}

