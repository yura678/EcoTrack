using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Models;
using Application.Features.ComplianceEvents.Commands.CloseComplianceEvent;
using Application.Features.ComplianceEvents.Commands.InvestigateComplianceEvent;
using Application.Features.ComplianceEvents.Commands.ReopenComplianceEvent;
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
    [HttpGet("compliance-events")]
    [ProducesOkApiResponseType<PageResult<ComplianceEventDto>>]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ComplianceEventListQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetPagedAsync(
            query.Status, query.EventType,
            query.EmissionSourceId, query.DeviceId,
            query.From, query.To,
            query.Page, query.PageSize, cancellationToken);

        return Ok(new PageResult<ComplianceEventDto>
        {
            Items = result.Items.Select(ComplianceEventDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("compliance-events/{id:guid}")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await queries.GetByIdAsync(id, cancellationToken);
        return entity.Match<ActionResult>(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            () => NotFound());
    }

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

    [HttpPatch("compliance-events/{id:guid}/reopen")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var input = new ReopenComplianceEventCommand { Id = id };
        var result = await sender.Send(input, cancellationToken);
        return result.Match(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            e => e.ToObjectResult());
    }
}
