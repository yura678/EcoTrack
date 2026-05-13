using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.ComplianceEvents.Commands.CloseComplianceEvent;
using Application.Features.ComplianceEvents.Commands.InvestigateComplianceEvent;
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
public class ComplianceEventController(
    IComplianceEventQueries queries,
    ISender sender) : BaseController
{
    [HttpGet("measurements/{measurementId:guid}/compliance-events")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceEventDto>>]
    public async Task<IActionResult> GetByMeasurementId(
        Guid measurementId,
        CancellationToken cancellationToken)
    {
        var entities = await queries.GetByMeasurementIdAsync(measurementId, cancellationToken);
        return Ok(entities.Select(ComplianceEventDto.FromDomainModel).ToList());
    }

    [HttpPatch("compliance-events/{id:guid}/close")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var input = new CloseComplianceEventCommand { Id = id };
        var result = await sender.Send(input, cancellationToken);
        return result.Match(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            e => e.ToObjectResult());
    }

    [HttpPatch("compliance-events/{id:guid}/investigating")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> Investigate(Guid id, CancellationToken cancellationToken)
    {
        var input = new InvestigateComplianceEventCommand { Id = id };
        var result = await sender.Send(input, cancellationToken);
        return result.Match(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            e => e.ToObjectResult());
    }
}
