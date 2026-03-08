using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Models;
using Application.Features.MonitoringPlans.Commands;
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
public class MonitoringPlanController(
    IMonitoringPlanQueries monitoringPlanQueries,
    ISender sender) : BaseController
{
    [HttpGet("installations/{installationId:guid}/monitoring-plans")]
    [ProducesOkApiResponseType<PageResult<MonitoringPlanDto>>]
    public async Task<IActionResult> GetMonitoringPlans(
        [FromRoute] Guid installationId,
        [FromQuery] MonitoringPlanQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await monitoringPlanQueries.GetPagedAsync(
            installationId,
            query.From, query.To, query.Page, query.PageSize,
            cancellationToken);

        return Ok(new PageResult<MonitoringPlanDto>
        {
            Items = result.Items.Select(MonitoringPlanDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }


    [HttpGet("monitoring-plans/{id:guid}")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> GetMonitoringPlan(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await monitoringPlanQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            () => NotFound());
    }

    [HttpPost("installations/{installationId:guid}/monitoring-plans")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> CreateMonitoringPlan(
        [FromRoute] Guid installationId,
        [FromBody] CreateMonitoringPlanDto request,
        CancellationToken cancellationToken)
    {
        var input = new CreateMonitoringPlanCommand
        {
            InstallationId = installationId,
            Version = request.Version,
            Notes = request.Notes,
            MonitoringRequirements = request.MonitoringRequirements!
                .Select(x => new MonitoringRequirementCommandDto(
                    x.EmissionSourceId,
                    x.PollutantId,
                    x.Frequency
                ))
                .ToList()
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpPut("monitoring-plans/{id:guid}")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> UpdateMonitoringPlan(
        [FromRoute] Guid id,
        [FromBody] UpdateMonitoringPlanDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateMonitoringPlanCommand
        {
            Id = id,
            Version = request.Version,
            Notes = request.Notes,
            Requirements =
                request.MonitoringRequirements!
                    .Select(x => new UpdateMonitoringRequirementCommandDto(
                        x.Id,
                        x.PollutantId,
                        x.EmissionSourceId,
                        x.Frequency)).ToList()
        };
        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpPatch("monitoring-plans/{id:guid}/archive")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> ArchiveMonitoringPlan(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new ArchiveMonitoringPlanCommand
        {
            Id = id
        };

        var archivedEntity = await sender.Send(input, cancellationToken);

        return archivedEntity.Match(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpPatch("monitoring-plans/{id:guid}/activate")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> ActivateMonitoringPlan(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new ActivateMonitoringPlanCommand
        {
            Id = id
        };

        var activatedEntity = await sender.Send(input, cancellationToken);

        return activatedEntity.Match(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpDelete("monitoring-plans/{id:guid}")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> DeleteMonitoringPlan(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeleteMonitoringPlanCommand
        {
            Id = id
        };

        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpPatch("monitoring-plans/{id:guid}/revoke")]
    [ProducesOkApiResponseType<MonitoringPlanDto>]
    public async Task<IActionResult> RevokeMonitoringPlan(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new RevokeMonitoringPlanCommand
        {
            Id = id
        };

        var revokedEntity = await sender.Send(input, cancellationToken);

        return revokedEntity.Match(
            m => Ok(MonitoringPlanDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }
}