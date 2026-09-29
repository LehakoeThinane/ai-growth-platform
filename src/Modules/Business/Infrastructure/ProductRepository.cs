using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Domain.Entities;
using AiGrowthPlatform.Business.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiGrowthPlatform.Business.Infrastructure.Repositories;

public sealed class ProductRepository(BusinessDbContext db) : IProductRepository
{
    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        await db.Products.AddAsync(product, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Product?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, cancellationToken);

    public async Task<IReadOnlyList<Product>> ListAsync(Guid organizationId, int skip, int take, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);

    public Task<bool> AnyForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(x => x.OrganizationId == organizationId, cancellationToken);

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        db.Products.Update(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Product product, CancellationToken cancellationToken)
    {
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }
}
