using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Infrastructure.Data;
using AiGrowthPlatform.Business.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiGrowthPlatform.Business.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BusinessDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("BusinessDatabase")));

        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}
