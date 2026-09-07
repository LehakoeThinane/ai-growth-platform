namespace AiGrowthPlatform.Business.Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public decimal? Price { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Product()
    {
        // Required by EF Core
        Name = string.Empty;
    }

    public Product(
        Guid organizationId,
        string name,
        string? description = null,
        decimal? price = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Product name is required.",
                nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentException(
                "Product price cannot be negative.",
                nameof(price));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        CreatedAt = DateTime.UtcNow;
    }
}