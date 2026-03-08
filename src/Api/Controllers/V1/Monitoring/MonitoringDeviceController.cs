using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Models;
using Application.Features.MonitoringDevices.Commands;
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
public class MonitoringDevicesController(
    IMonitoringDeviceQueries monitoringDeviceQueries,
    ISender sender) : BaseController
{
    [HttpGet("installations/{installationId:guid}/monitoring-devices")]
    [ProducesOkApiResponseType<PageResult<MonitoringDeviceDto>>]
    public async Task<IActionResult> GetMonitoringDevices(
        [FromRoute] Guid installationId,
        [FromQuery] MonitoringDeviceQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await monitoringDeviceQueries.GetPagedAsync(
            installationId,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(new PageResult<MonitoringDeviceDto>
        {
            Items = result.Items.Select(MonitoringDeviceDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("monitoring-devices/{id:guid}")]
    [ProducesOkApiResponseType<MonitoringDeviceDto>]
    public async Task<IActionResult> GetMonitoringDeviceById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await monitoringDeviceQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            d => Ok(MonitoringDeviceDto.FromDomainModel(d)),
            () => NotFound());
    }


    [HttpPost("installations/{installationId:guid}/monitoring-devices")]
    [ProducesOkApiResponseType<MonitoringDeviceDto>]
    public async Task<IActionResult> CreateMonitoringDevice(
        [FromRoute] Guid installationId,
        [FromBody] CreateMonitoringDeviceDto request,
        CancellationToken cancellationToken)
    {
        var input = new CreateMonitoringDeviceCommand
        {
            EmissionSourceId = request.EmissionSourceId,
            InstallationId = installationId,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            Type = request.Type,
            IsOnline = request.IsOnline,
            Notes = request.Notes
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            d => Ok(MonitoringDeviceDto.FromDomainModel(d)),
            e => e.ToObjectResult());
    }

    [HttpPut("monitoring-devices/{id:guid}")]
    [ProducesOkApiResponseType<MonitoringDeviceDto>]
    public async Task<IActionResult> UpdateMonitoringDevice(
        [FromRoute] Guid id,
        [FromBody] UpdateMonitoringDeviceDto request,
        CancellationToken cancellationToken)
    {
        var input = new UpdateMonitoringDeviceCommand
        {
            Id = id,
            EmissionSourceId = request.EmissionSourceId,
            IsOnline = request.IsOnline,
            Notes = request.Notes
        };
        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            d => Ok(MonitoringDeviceDto.FromDomainModel(d)),
            e => e.ToObjectResult());
    }

    [HttpDelete("monitoring-devices/{id:guid}")]
    [ProducesOkApiResponseType<MonitoringDeviceDto>]
    public async Task<IActionResult> DeleteMonitoringDevice(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new DeleteMonitoringDeviceCommand
        {
            Id = id
        };
        var deletedEntity = await sender.Send(input, cancellationToken);

        return deletedEntity.Match(
            d => Ok(MonitoringDeviceDto.FromDomainModel(d)),
            e => e.ToObjectResult());
    }
}