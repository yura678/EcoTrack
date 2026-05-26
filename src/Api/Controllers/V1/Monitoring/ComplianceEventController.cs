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
using Domain.Entities.Monitoring;
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
    ICurrentViolationProbe violationProbe,
    ISender sender) : BaseController
{
    [HttpGet("compliance-events")]
    [ProducesOkApiResponseType<PageResult<ComplianceEventDto>>]
    public Task<IActionResult> GetPaged(
        [FromQuery] ComplianceEventListQueryDto query,
        CancellationToken cancellationToken)
        => GetPagedScopedAsync(query, installationId: null, siteId: null, cancellationToken);

    [HttpGet("installations/{installationId:guid}/compliance-events")]
    [ProducesOkApiResponseType<PageResult<ComplianceEventDto>>]
    public Task<IActionResult> GetByInstallation(
        [FromRoute] Guid installationId,
        [FromQuery] ComplianceEventListQueryDto query,
        CancellationToken cancellationToken)
        => GetPagedScopedAsync(query, installationId, siteId: null, cancellationToken);

    [HttpGet("sites/{siteId:guid}/compliance-events")]
    [ProducesOkApiResponseType<PageResult<ComplianceEventDto>>]
    public Task<IActionResult> GetBySite(
        [FromRoute] Guid siteId,
        [FromQuery] ComplianceEventListQueryDto query,
        CancellationToken cancellationToken)
        => GetPagedScopedAsync(query, installationId: null, siteId, cancellationToken);

    [HttpGet("compliance-events/{id:guid}")]
    [ProducesOkApiResponseType<ComplianceEventDetailDto>]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await queries.GetDetailByIdAsync(id, cancellationToken);
        return await entity.Match<Task<ActionResult>>(
            async view => Ok(ComplianceEventDetailDto.FromDomainModel(
                view, await ProbeIfOpenAsync(view.Event, cancellationToken))),
            () => Task.FromResult<ActionResult>(NotFound()));
    }

    [HttpGet("measurements/{measurementId:guid}/compliance-events")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceEventDto>>]
    public async Task<IActionResult> GetByMeasurementId(
        Guid measurementId,
        CancellationToken cancellationToken)
    {
        var entities = await queries.GetByMeasurementIdAsync(measurementId, cancellationToken);
        return Ok(entities.Select(e => ComplianceEventDto.FromDomainModel(e)).ToList());
    }

    [HttpPatch("compliance-events/{id:guid}/close")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> Close(
        Guid id,
        [FromBody] CloseComplianceEventDto body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CloseComplianceEventCommand { Id = id, Reason = body.Reason, Note = body.Note },
            cancellationToken);
        return result.Match(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            e => e.ToObjectResult());
    }

    [HttpPatch("compliance-events/{id:guid}/investigating")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> Investigate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InvestigateComplianceEventCommand { Id = id }, cancellationToken);
        return result.Match(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            e => e.ToObjectResult());
    }

    [HttpPatch("compliance-events/{id:guid}/reopen")]
    [ProducesOkApiResponseType<ComplianceEventDto>]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReopenComplianceEventCommand { Id = id }, cancellationToken);
        return result.Match(
            ev => Ok(ComplianceEventDto.FromDomainModel(ev)),
            e => e.ToObjectResult());
    }

    private async Task<IActionResult> GetPagedScopedAsync(
        ComplianceEventListQueryDto query,
        Guid? installationId,
        Guid? siteId,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetPagedAsync(
            query.Status, query.EventType,
            query.EmissionSourceId, query.DeviceId,
            installationId, siteId,
            query.From, query.To,
            query.Page, query.PageSize, cancellationToken);

        // Only probe Open events — Closed/Investigating represent operator decisions
        // and the "currently violating" hint adds no value once the event has been actioned.
        var probe = await violationProbe.ProbeAsync(
            result.Items.Where(e => e.Status == ComplianceEventStatus.Open).ToList(),
            cancellationToken);

        return Ok(new PageResult<ComplianceEventDto>
        {
            Items = result.Items
                .Select(e => ComplianceEventDto.FromDomainModel(
                    e, probe.TryGetValue(e.Id, out var v) ? v : null))
                .ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    private async Task<bool?> ProbeIfOpenAsync(ComplianceEvent ev, CancellationToken cancellationToken)
    {
        if (ev.Status != ComplianceEventStatus.Open) return null;
        var probe = await violationProbe.ProbeAsync([ev], cancellationToken);
        return probe.TryGetValue(ev.Id, out var v) ? v : null;
    }
}
