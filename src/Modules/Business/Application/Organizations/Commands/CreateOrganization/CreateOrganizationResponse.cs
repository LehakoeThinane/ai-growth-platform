namespace AiGrowthPlatform.Business.Application.Organizations.Commands.CreateOrganization;

public sealed record CreateOrganizationResponse(
    Guid Id,
    string Name,
    string? WebsiteUrl,
    DateTime CreatedAt
);
