using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.ExceedanceEvents.Commands.CloseExceedanceEvent;
using Application.Features.ExceedanceEvents.Commands.InvestigateExceedanceEvent;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Monitoring;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}")]
[ApiController]
public class ExceedanceEventController(
    IExceedanceEventQueries exceedanceEventQueries,
    ISender sender) : BaseController
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

    [HttpPatch("exceedance-events/{exceedanceEventId:guid}/close")]
    [ProducesOkApiResponseType<ExceedanceEventDto>]
    public async Task<IActionResult> CloseExceedanceEvent(
        Guid exceedanceEventId,
        CancellationToken cancellationToken)
    {
        var input = new CloseExceedanceEventCommand { Id = exceedanceEventId };
        var result = await sender.Send(input, cancellationToken);

        return result.Match(
            ev => Ok(ev),
            e => e.ToObjectResult());
    }

    [HttpPatch("exceedance-events/{exceedanceEventId:guid}/Investigating")]
    [ProducesOkApiResponseType<ExceedanceEventDto>]
    public async Task<IActionResult> InvestigateExceedanceEvent(
        Guid exceedanceEventId,
        CancellationToken cancellationToken)
    {
        var input = new InvestigateExceedanceEventCommand { Id = exceedanceEventId };
        var result = await sender.Send(input, cancellationToken);

        return result.Match(
            ev => Ok(ev),
            e => e.ToObjectResult());
    }
}