using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Enterprises;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Enterprise;

// Sectors are a global reference dictionary — every authenticated user needs to read them
// for dropdowns and labelling. Gating with DynamicPermission would force every new tenant
// role to carry an extra claim just to render the registration form / enterprise card.
// The list endpoint is intentionally anonymous so the self-signup form can populate its
// sector picker before the user has a token; the by-id endpoint stays authenticated.
[ApiVersion("1")]
[Authorize]
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
    [AllowAnonymous]
    [ProducesOkApiResponseType<IReadOnlyList<SectorDto>>]
    public async Task<IActionResult> GetSectors(
        CancellationToken cancellationToken)
    {
        var entities = await sectorQueries.GetAllAsync(cancellationToken);

        return Ok(entities.Select(SectorDto.FromDomainModel).ToList());
    }
}