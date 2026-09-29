using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Business.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiGrowthPlatform.Api.Agents;

public sealed class BusinessCatalog(BusinessDbContext db) : IBusinessCatalog
{
    public Task<OrganizationInfo?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken) =>
        db.Organizations.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new OrganizationInfo(x.Id, x.Name, x.WebsiteUrl)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductInfo>> GetProductsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Take(100).Select(x => new ProductInfo(x.Id, x.Name, x.Description, x.Price)).ToListAsync(cancellationToken);
}
