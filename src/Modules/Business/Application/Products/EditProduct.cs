using AiGrowthPlatform.Business.Application.Abstractions;
using MediatR;

namespace AiGrowthPlatform.Business.Application.Products;

public sealed record UpdateProductCommand(Guid OrganizationId, Guid Id, string Name, string? Description, decimal? Price)
    : IRequest<ProductResponse>;

public sealed record DeleteProductCommand(Guid OrganizationId, Guid Id) : IRequest;

public sealed class UpdateProductHandler(IProductRepository products) : IRequestHandler<UpdateProductCommand, ProductResponse>
{
    public async Task<ProductResponse> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.OrganizationId, request.Id, cancellationToken)
            ?? throw new BusinessNotFoundException("Product not found.");
        product.Update(request.Name, request.Description, request.Price);
        await products.UpdateAsync(product, cancellationToken);
        return ProductResponse.From(product);
    }
}

public sealed class DeleteProductHandler(IProductRepository products) : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.OrganizationId, request.Id, cancellationToken)
            ?? throw new BusinessNotFoundException("Product not found.");
        await products.DeleteAsync(product, cancellationToken);
    }
}
