using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Enterprises;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Enterprise;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/sectors")]
[ApiController]
public class SectorController(
    ISectorQueries sectorQueries) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<SectorDto>]
    public async Task<IActionResult> GetSectorById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await sectorQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            s => Ok(SectorDto.FromDomainModel(s)),
            () => NotFound()
        );
    }

    [HttpGet]
    [ProducesOkApiResponseType<IReadOnlyList<SectorDto>>]
    public async Task<IActionResult> GetSectors(
        CancellationToken cancellationToken)
    {
        var entities = await sectorQueries.GetAllAsync(cancellationToken);

        return Ok(entities.Select(SectorDto.FromDomainModel).ToList());
    }
}