using AiGrowthPlatform.Business.Domain.Entities;

namespace AiGrowthPlatform.Business.Application.Abstractions;

public interface IOrganizationRepository
{
    Task<IReadOnlyList<Organization>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task AddAsync(
        Organization organization,
        CancellationToken cancellationToken = default);

    Task<Organization?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
