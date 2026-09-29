using System.ComponentModel.DataAnnotations;
using AiGrowthPlatform.Business.Application.Abstractions;
using AiGrowthPlatform.Business.Application.Products;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AiGrowthPlatform.Api.Controllers;

[ApiController]
[Route("api/organizations/{organizationId:guid}/products")]
public sealed class ProductsController(IMediator mediator, IOrganizationRepository organizations, IProductRepository products) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(Guid organizationId, CreateProductRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateProductCommand(organizationId, request.Name, request.Description, request.Price), ct);
        return CreatedAtAction(nameof(Get), new { organizationId, id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Get(Guid organizationId, Guid id, CancellationToken ct) =>
        await products.GetByIdAsync(organizationId, id, ct) is { } product ? Ok(ProductResponse.From(product)) : NotFound();

    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct,
        [FromQuery, Range(0, int.MaxValue)] int skip = 0, [FromQuery, Range(1, 100)] int take = 50)
    {
        if (await organizations.GetByIdAsync(organizationId, ct) is null) return NotFound();
        return Ok((await products.ListAsync(organizationId, skip, take, ct)).Select(ProductResponse.From));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(Guid organizationId, Guid id, CreateProductRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateProductCommand(organizationId, id, request.Name, request.Description, request.Price), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(organizationId, id), ct);
        return NoContent();
    }
}

public sealed record CreateProductRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(2000)] string? Description,
    decimal? Price);
