using Microsoft.Extensions.DependencyInjection;

namespace AiGrowthPlatform.Business.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(
                typeof(DependencyInjection).Assembly);
        });

        return services;
    }
}
