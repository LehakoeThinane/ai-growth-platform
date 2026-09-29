using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Domain.Entities;
using MediatR;

namespace AiGrowthPlatform.Business.Application.Products;

public sealed record CreateProductCommand(Guid OrganizationId, string Name, string? Description, decimal? Price)
    : IRequest<ProductResponse>;

public sealed record ProductResponse(Guid Id, Guid OrganizationId, string Name, string? Description, decimal? Price, DateTime CreatedAt)
{
    public static ProductResponse From(Product product) => new(product.Id, product.OrganizationId, product.Name,
        product.Description, product.Price, product.CreatedAt);
}

public sealed class CreateProductHandler(IOrganizationRepository organizations, IProductRepository products)
    : IRequestHandler<CreateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(request.OrganizationId, request.Name, request.Description, request.Price);
        if (await organizations.GetByIdAsync(request.OrganizationId, cancellationToken) is null)
            throw new BusinessNotFoundException("Organization not found.");
        await products.AddAsync(product, cancellationToken);
        return ProductResponse.From(product);
    }
}
