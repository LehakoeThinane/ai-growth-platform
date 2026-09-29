using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Domain.Entities;
using AiGrowthPlatform.Business.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AiGrowthPlatform.Business.Infrastructure.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly BusinessDbContext _dbContext;

    public OrganizationRepository(BusinessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Organizations.AddAsync(
            organization,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Organization?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Organizations
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> ListAsync(int skip, int take, CancellationToken cancellationToken) =>
        await _dbContext.Organizations.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);

    public async Task UpdateAsync(Organization organization, CancellationToken cancellationToken)
    {
        _dbContext.Organizations.Update(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Organization organization, CancellationToken cancellationToken)
    {
        _dbContext.Organizations.Remove(organization);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
