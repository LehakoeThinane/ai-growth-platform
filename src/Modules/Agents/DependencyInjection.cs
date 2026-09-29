using AiGrowthPlatform.Agents.Models;
using AiGrowthPlatform.Agents.Persistence;
using AiGrowthPlatform.Agents.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AiGrowthPlatform.Agents;

public static class DependencyInjection
{
    public static IServiceCollection AddAgents(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<AgentOptions>().Bind(configuration.GetSection("Agents"))
            .Validate(x => x.Mode is "Demo" or "OpenAI", "Agents:Mode must be Demo or OpenAI.")
            .Validate(x => x.CatalogSource is "Auto" or "Demo" or "Database", "Agents:CatalogSource must be Auto, Demo, or Database.")
            .Validate(x => x.MaxSteps is >= 1 and <= 30, "Agents:MaxSteps must be 1–30.")
            .Validate(x => x.TimeoutSeconds is >= 1 and <= 300, "Agents:TimeoutSeconds must be 1–300.")
            .Validate(x => x.Mode != "OpenAI" || (!string.IsNullOrWhiteSpace(x.ApiKey) && !string.IsNullOrWhiteSpace(x.Model)),
                "OpenAI mode requires Agents:ApiKey and Agents:Model.")
            .Validate(x => environment.IsDevelopment() || (x.Mode == "OpenAI" && x.AccessKey.Length >= 32),
                "Outside Development, configure OpenAI mode and an Agents:AccessKey of at least 32 characters.")
            .ValidateOnStart();
        services.PostConfigure<AgentOptions>(x =>
        {
            if (!Path.IsPathRooted(x.DataDirectory)) x.DataDirectory = Path.Combine(environment.ContentRootPath, x.DataDirectory);
        });
        services.AddSingleton<AgentStore>();
        services.AddScoped<WorkflowTools>();
        services.AddScoped<AgentRunner>();
        services.AddHttpClient<OpenAiAgentModel>(client => client.Timeout = TimeSpan.FromSeconds(90));
        services.AddScoped<IAgentModel>(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value.Mode == "Demo"
            ? new DemoAgentModel() : sp.GetRequiredService<OpenAiAgentModel>());
        services.AddHostedService<AgentStartup>();
        return services;
    }

    private sealed class AgentStartup(AgentStore store, IOptions<AgentOptions> options) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await store.MarkInterruptedRunsAsync(cancellationToken);
            if (options.Value.EffectiveCatalogSource != "Demo") return;
            foreach (var ticket in new[]
            {
                new SupportTicket("INC-1042", DemoCatalog.OrganizationId, "Service recovered after transient outage", "service_recovered", true, "open"),
                new SupportTicket("INC-1043", DemoCatalog.OrganizationId, "Unexplained billing discrepancy", "billing", false, "open")
            })
                if (await store.GetTicketAsync(ticket.OrganizationId, ticket.Id, cancellationToken) is null)
                    await store.CreateTicketAsync(ticket, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
