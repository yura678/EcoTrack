using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.CalibrationRecords.Commands;
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
public class CalibrationRecordController(
    ICalibrationRecordQueries queries,
    ISender sender) : BaseController
{
    [HttpGet("monitoring-devices/{deviceId:guid}/calibrations")]
    [ProducesOkApiResponseType<IReadOnlyList<CalibrationRecordDto>>]
    public async Task<IActionResult> GetByDevice(
        [FromRoute] Guid deviceId,
        CancellationToken cancellationToken)
    {
        var entities = await queries.GetByDeviceIdAsync(deviceId, cancellationToken);
        return Ok(entities.Select(CalibrationRecordDto.FromDomainModel).ToList());
    }

    [HttpGet("calibrations/{id:guid}")]
    [ProducesOkApiResponseType<CalibrationRecordDto>]
    [ProducesNotFoundApiResponseType]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await queries.GetByIdAsync(id, cancellationToken);
        return entity.Match<ActionResult>(
            c => Ok(CalibrationRecordDto.FromDomainModel(c)),
            () => NotFound());
    }

    [HttpPost("monitoring-devices/{deviceId:guid}/calibrations")]
    [ProducesOkApiResponseType<CalibrationRecordDto>]
    public async Task<IActionResult> Create(
        [FromRoute] Guid deviceId,
        [FromBody] CreateCalibrationRecordDto request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCalibrationRecordCommand
        {
            DeviceId = deviceId,
            Type = request.Type,
            PerformedAt = request.PerformedAt,
            NextDueAt = request.NextDueAt,
            Result = request.Result,
            PerformedBy = request.PerformedBy,
            CertificateNumber = request.CertificateNumber,
            Notes = request.Notes,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            c => Ok(CalibrationRecordDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }

    [HttpPut("calibrations/{id:guid}")]
    [ProducesOkApiResponseType<CalibrationRecordDto>]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCalibrationRecordDto request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCalibrationRecordCommand
        {
            Id = id,
            Type = request.Type,
            PerformedAt = request.PerformedAt,
            NextDueAt = request.NextDueAt,
            Result = request.Result,
            PerformedBy = request.PerformedBy,
            CertificateNumber = request.CertificateNumber,
            Notes = request.Notes,
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            c => Ok(CalibrationRecordDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }

    [HttpDelete("calibrations/{id:guid}")]
    [ProducesOkApiResponseType<CalibrationRecordDto>]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCalibrationRecordCommand { Id = id };
        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            c => Ok(CalibrationRecordDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }
}
