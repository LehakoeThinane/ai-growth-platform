using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Domain.Entities;
using MediatR;

namespace AiGrowthPlatform.Business.Application.Organizations.Commands.CreateOrganization;

public sealed class CreateOrganizationHandler
    : IRequestHandler<CreateOrganizationCommand, CreateOrganizationResponse>
{
    private readonly IOrganizationRepository _organizationRepository;

    public CreateOrganizationHandler(
        IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<CreateOrganizationResponse> Handle(
        CreateOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var organization = new Organization(
            request.Name,
            request.WebsiteUrl);

        await _organizationRepository.AddAsync(
            organization,
            cancellationToken);

        return new CreateOrganizationResponse(
            organization.Id,
            organization.Name,
            organization.WebsiteUrl,
            organization.CreatedAt);
    }
}
