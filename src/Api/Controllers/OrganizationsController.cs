using AiGrowthPlatform.Business.Application.Organizations.Commands.CreateOrganization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using AiGrowthPlatform.Business.Application.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace AiGrowthPlatform.Api.Controllers;

[ApiController]
[Route("api/organizations")]
public class OrganizationsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromServices] IOrganizationRepository repository,
        CancellationToken cancellationToken, [FromQuery, Range(0, int.MaxValue)] int skip = 0,
        [FromQuery, Range(1, 100)] int take = 50) => Ok(await repository.ListAsync(skip, take, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] IOrganizationRepository repository,
        CancellationToken cancellationToken) => await repository.GetByIdAsync(id, cancellationToken) is { } organization
            ? Ok(organization) : NotFound();

    private readonly IMediator _mediator;

    public OrganizationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<CreateOrganizationResponse>> Create(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            command,
            cancellationToken);

        return Created(
            $"/api/organizations/{result.Id}",
            result);
    }
}
