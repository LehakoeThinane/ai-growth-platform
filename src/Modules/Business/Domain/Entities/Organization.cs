namespace AiGrowthPlatform.Business.Domain.Entities;

public class Organization
{
    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? WebsiteUrl { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Organization()
    {
        // Required by EF Core
        Name = string.Empty;
    }

    public Organization(string name, string? websiteUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessValidationException("Organization name is required.");
        }

        if (name.Trim().Length > 200)
            throw new BusinessValidationException("Organization name must be at most 200 characters.");
        websiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim();
        if (websiteUrl is not null && (websiteUrl.Length > 500 ||
            !Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new BusinessValidationException("Website URL must be an absolute HTTP or HTTPS URL of at most 500 characters.");

        Id = Guid.NewGuid();
        Name = name.Trim();
        WebsiteUrl = websiteUrl;
        CreatedAt = DateTime.UtcNow;
    }
}
