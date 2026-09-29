namespace AiGrowthPlatform.Agents.Workflows;

public sealed class DemoCatalog : IBusinessCatalog
{
    public static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Task<OrganizationInfo?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(id == OrganizationId ? new OrganizationInfo(id, "Example Growth Co", "https://example.com") : null);

    public Task<IReadOnlyList<ProductInfo>> GetProductsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductInfo>>(organizationId == OrganizationId ?
        [new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Starter consultation", "A one-hour business consultation", 500),
         new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Growth workshop", null, null)] : []);
}
