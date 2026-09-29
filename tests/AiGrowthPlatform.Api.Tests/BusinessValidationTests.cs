using AiGrowthPlatform.Agents;
using AiGrowthPlatform.Business.Domain;
using AiGrowthPlatform.Business.Domain.Entities;
using Xunit;

namespace AiGrowthPlatform.Api.Tests;

public sealed class BusinessValidationTests
{
    [Theory]
    [InlineData("-0.01")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void Invalid_prices_are_rejected_before_database_rounding(string amount)
    {
        var price = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<BusinessValidationException>(() => new Product(Guid.NewGuid(), "Product", price: price));
    }

    [Fact]
    public void Names_and_descriptions_obey_database_limits()
    {
        Assert.Throws<BusinessValidationException>(() => new Product(Guid.NewGuid(), "  "));
        Assert.Throws<BusinessValidationException>(() => new Product(Guid.NewGuid(), new string('x', 201)));
        Assert.Throws<BusinessValidationException>(() => new Product(Guid.NewGuid(), "Product", new string('x', 2001)));
        Assert.Throws<BusinessValidationException>(() => new Product(Guid.Empty, "Product"));
        Assert.Throws<BusinessValidationException>(() => new Organization(new string('x', 201)));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("example.com")]
    public void Website_must_be_an_absolute_web_url(string url) =>
        Assert.Throws<BusinessValidationException>(() => new Organization("Business", url));

    [Fact]
    public void Optional_values_and_zero_price_are_valid()
    {
        var organization = new Organization(" Business ", " ");
        var product = new Product(organization.Id, " Product ", " Description ", 0);
        Assert.Equal("Business", organization.Name);
        Assert.Null(organization.WebsiteUrl);
        Assert.Equal("Product", product.Name);
        Assert.Equal("Description", product.Description);
        Assert.Equal(0, product.Price);
        Assert.Null(new Product(organization.Id, "Unpriced").Price);
    }

    [Theory]
    [InlineData("Demo", "Auto", "Demo")]
    [InlineData("OpenAI", "Auto", "Database")]
    [InlineData("Demo", "Database", "Database")]
    [InlineData("OpenAI", "Demo", "Demo")]
    public void Model_and_catalogue_can_be_selected_independently(string mode, string selected, string expected) =>
        Assert.Equal(expected, new AgentOptions { Mode = mode, CatalogSource = selected }.EffectiveCatalogSource);
}
