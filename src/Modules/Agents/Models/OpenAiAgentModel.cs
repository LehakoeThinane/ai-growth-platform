using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiGrowthPlatform.Agents.Models;

public sealed class OpenAiAgentModel(HttpClient client, IOptions<AgentOptions> options) : IAgentModel
{
    public string Mode => "OpenAI";
    public IAgentSession CreateSession() => new Session(client, options.Value);

    private sealed class Session(HttpClient client, AgentOptions options) : IAgentSession
    {
        private readonly List<JsonElement> _input = [];
        private ToolCall? _pending;

        public async Task<ToolCall> DecideAsync(AgentRun run, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Model))
                throw new AgentProviderException("Configure Agents:ApiKey and Agents:Model before using OpenAI mode.");
            if (_input.Count == 0)
                _input.Add(AgentJson.Element(new { role = "user", content = JsonSerializer.Serialize(new
                {
                    request = run.Request,
                    memory = run.LoadedMemory
                }, AgentJson.Options) }));
            if (_pending is not null)
            {
                var observation = run.Observations.Last();
                _input.Add(AgentJson.Element(new { type = "function_call_output", call_id = _pending.Id,
                    output = observation.Result.GetRawText() }));
                _pending = null;
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            request.Content = JsonContent.Create(new
            {
                model = options.Model,
                instructions = AgentInstructions.Text,
                input = _input,
                tools = tools.Select(x => new { type = "function", name = x.Name, description = x.Description,
                    parameters = x.Schema, strict = true }),
                tool_choice = "required",
                parallel_tool_calls = false,
                max_output_tokens = 4096,
                store = false,
                include = new[] { "reasoning.encrypted_content" }
            });
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new AgentProviderException($"Model provider returned HTTP {(int)response.StatusCode}. Check credentials, model access, and provider availability.");
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length > 1_000_000) throw new AgentProviderException("Model response exceeded the allowed size.");
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.GetProperty("status").GetString() != "completed")
                throw new AgentProviderException("Model response did not complete; no tool was executed.");
            var output = root.GetProperty("output").EnumerateArray().Select(x => x.Clone()).ToArray();
            var calls = output.Where(x => x.GetProperty("type").GetString() == "function_call").ToArray();
            if (calls.Length != 1) throw new AgentProviderException("Expected exactly one tool decision from the model.");
            var call = calls[0];
            // Replay the complete output, including encrypted reasoning items, as required by Responses.
            // These items remain ephemeral and are not written into the audit trail.
            _input.AddRange(output);
            _pending = new ToolCall(call.GetProperty("call_id").GetString()!, call.GetProperty("name").GetString()!,
                call.GetProperty("arguments").GetString()!);
            return _pending;
        }
    }
}
