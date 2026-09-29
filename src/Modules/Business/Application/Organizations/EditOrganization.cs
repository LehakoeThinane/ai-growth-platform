using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Domain.Entities;
using MediatR;

namespace AiGrowthPlatform.Business.Application.Organizations;

public sealed record UpdateOrganizationCommand(Guid Id, string Name, string? WebsiteUrl) : IRequest<Organization>;

public sealed record DeleteOrganizationCommand(Guid Id) : IRequest;

public sealed class UpdateOrganizationHandler(IOrganizationRepository organizations)
    : IRequestHandler<UpdateOrganizationCommand, Organization>
{
    public async Task<Organization> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessNotFoundException("Organization not found.");
        organization.Update(request.Name, request.WebsiteUrl);
        await organizations.UpdateAsync(organization, cancellationToken);
        return organization;
    }
}

public sealed class DeleteOrganizationHandler(IOrganizationRepository organizations, IProductRepository products)
    : IRequestHandler<DeleteOrganizationCommand>
{
    public async Task Handle(DeleteOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessNotFoundException("Organization not found.");
        // The foreign key also restricts deletion; this check gives a clear message in the common case.
        if (await products.AnyForOrganizationAsync(request.Id, cancellationToken))
            throw new BusinessConflictException("Delete this organization's products before deleting the organization.");
        await organizations.DeleteAsync(organization, cancellationToken);
    }
}
