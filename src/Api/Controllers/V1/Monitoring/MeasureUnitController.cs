using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Monitoring;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Monitoring;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/units")]
[ApiController]
public class MeasureUnitController(
    IMeasureUnitQueries measureUnitQueries) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<MeasureUnitDto>]
    public async Task<ActionResult<MeasureUnitDto>> GetMeasureUnitById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await measureUnitQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            u => Ok(MeasureUnitDto.FromDomainModel(u)),
            () => NotFound()
        );
    }

    [HttpGet]
    [ProducesOkApiResponseType<IReadOnlyList<MeasureUnitDto>>]
    public async Task<IActionResult> GetMeasureUnits(
        CancellationToken cancellationToken)
    {
        var entities = await measureUnitQueries.GetAllAsync(cancellationToken);

        return Ok(entities.Select(MeasureUnitDto.FromDomainModel).ToList());
    }
}