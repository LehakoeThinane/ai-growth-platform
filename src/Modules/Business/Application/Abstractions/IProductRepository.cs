using AiGrowthPlatform.Business.Domain.Entities;

namespace AiGrowthPlatform.Business.Application.Abstractions;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task<Product?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> ListAsync(Guid organizationId, int skip, int take, CancellationToken cancellationToken);
}
