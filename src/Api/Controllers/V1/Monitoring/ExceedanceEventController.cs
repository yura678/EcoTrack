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
[Route("api/v{version:apiVersion}")]
[ApiController]
public class ExceedanceEventController(
    IExceedanceEventQueries exceedanceEventQueries) : BaseController
{
    [HttpGet("measurements/{measurementId:guid}/exceedance-events")]
    [ProducesOkApiResponseType<IReadOnlyList<ExceedanceEventDto>>]
    public async Task<IActionResult> GetExceedanceEventByMeasurementId(
        Guid measurementId,
        CancellationToken cancellationToken)
    {
        var entities =
            await exceedanceEventQueries.GetByMeasurementIdAsync(measurementId, cancellationToken);

        return Ok(entities.Select(ExceedanceEventDto.FromDomainModel).ToList());
    }
}