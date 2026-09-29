using MediatR;

namespace AiGrowthPlatform.Business.Application.Organizations.Commands.CreateOrganization;

public sealed record CreateOrganizationCommand(
    string Name,
    string? WebsiteUrl
) : IRequest<CreateOrganizationResponse>;
