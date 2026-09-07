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
            throw new ArgumentException(
                "Organization name is required.",
                nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        WebsiteUrl = websiteUrl;
        CreatedAt = DateTime.UtcNow;
    }
}