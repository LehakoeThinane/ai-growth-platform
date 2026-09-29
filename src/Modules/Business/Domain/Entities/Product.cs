using System.Diagnostics.CodeAnalysis;

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
            throw new BusinessValidationException("Organization ID is required.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedAt = DateTime.UtcNow;
        Update(name, description, price);
    }

    [MemberNotNull(nameof(Name))]
    public void Update(string name, string? description, decimal? price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessValidationException("Product name is required.");
        }

        if (name.Trim().Length > 200 || description?.Trim().Length > 2000)
        {
            throw new BusinessValidationException("Product names must be at most 200 characters and descriptions at most 2000.");
        }

        if (price is { } amount && (amount < 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount))
            throw new BusinessValidationException("Price must be non-negative, have at most two decimal places, and fit within 16 whole-number digits.");

        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
    }
}
