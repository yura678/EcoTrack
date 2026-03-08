using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Emissions;
using Infrastructure.Identity.PermissionManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.EmissionSources;

[Route("api/v{version:apiVersion}/pollutants")]
[Authorize(ConstantPolicies.DynamicPermission)]
[ApiController]
public class PollutantController(
    IPollutantQueries pollutantQueries) : BaseController
{
    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<PollutantDto>]
    public async Task<IActionResult> GetPollutantById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await pollutantQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            p => Ok(PollutantDto.FromDomainModel(p)),
            () => NotFound()
        );
    }

    [HttpGet]
    [ProducesOkApiResponseType<IReadOnlyList<PollutantDto>>]
    public async Task<IActionResult> GetPollutants(
        CancellationToken cancellationToken)
    {
        var entities = await pollutantQueries.GetAllAsync(cancellationToken);

        return Ok(entities.Select(PollutantDto.FromDomainModel).ToList());
    }
}