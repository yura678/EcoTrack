using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.DevicePollutantCapabilities.Commands;
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
public class DevicePollutantCapabilityController(
    IDevicePollutantCapabilityQueries queries,
    ISender sender) : BaseController
{
    [HttpGet("monitoring-devices/{deviceId:guid}/capabilities")]
    [ProducesOkApiResponseType<IReadOnlyList<DevicePollutantCapabilityDto>>]
    public async Task<IActionResult> GetByDevice(
        [FromRoute] Guid deviceId,
        CancellationToken cancellationToken)
    {
        var entities = await queries.GetByDeviceIdAsync(deviceId, cancellationToken);
        return Ok(entities.Select(DevicePollutantCapabilityDto.FromDomainModel).ToList());
    }

    [HttpGet("capabilities/{id:guid}")]
    [ProducesOkApiResponseType<DevicePollutantCapabilityDto>]
    [ProducesNotFoundApiResponseType]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await queries.GetByIdAsync(id, cancellationToken);
        return entity.Match<ActionResult>(
            c => Ok(DevicePollutantCapabilityDto.FromDomainModel(c)),
            () => NotFound());
    }

    [HttpPost("monitoring-devices/{deviceId:guid}/capabilities")]
    [ProducesOkApiResponseType<DevicePollutantCapabilityDto>]
    public async Task<IActionResult> Create(
        [FromRoute] Guid deviceId,
        [FromBody] CreateDevicePollutantCapabilityDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateDevicePollutantCapabilityCommand
        {
            DeviceId = deviceId,
            PollutantId = request.PollutantId,
            RangeMin = request.RangeMin,
            RangeMax = request.RangeMax,
            RangeUnitId = request.RangeUnitId,
            AccuracyClass = request.AccuracyClass,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            c => Ok(DevicePollutantCapabilityDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }

    [HttpPut("capabilities/{id:guid}")]
    [ProducesOkApiResponseType<DevicePollutantCapabilityDto>]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateDevicePollutantCapabilityDto request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDevicePollutantCapabilityCommand
        {
            Id = id,
            RangeMin = request.RangeMin,
            RangeMax = request.RangeMax,
            RangeUnitId = request.RangeUnitId,
            AccuracyClass = request.AccuracyClass,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            c => Ok(DevicePollutantCapabilityDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }

    [HttpDelete("capabilities/{id:guid}")]
    [ProducesOkApiResponseType<DevicePollutantCapabilityDto>]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteDevicePollutantCapabilityCommand { Id = id };
        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            c => Ok(DevicePollutantCapabilityDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }
}
