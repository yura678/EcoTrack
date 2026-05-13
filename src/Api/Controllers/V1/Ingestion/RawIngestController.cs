using Api.Attributes;
using Api.Dtos;
using Api.Modules.Validators.RawIngest;
using Application.Common.Interfaces.Ingestion;
using Asp.Versioning;
using Domain.Entities.Monitoring;
using FluentValidation;
using Infrastructure.Identity.Ingestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Ingestion;

[ApiVersion("1")]
[Authorize(AuthenticationSchemes = DeviceHmacAuthDefaults.Scheme)]
[Route("api/v{version:apiVersion}/ingest")]
[ApiController]
public class RawIngestController(
    IRawMeasurementWriter measurementWriter,
    IRawProcessParameterWriter parameterWriter) : ControllerBase
{
    private Guid CurrentDeviceId =>
        Guid.Parse(User.FindFirst("DeviceId")!.Value);

    [HttpPost("measurements")]
    [ProducesOkApiResponseType<IngestBatchResult>]
    public async Task<IActionResult> IngestMeasurements(
        [FromBody] IReadOnlyList<RawMeasurementIngestDto> batch,
        CancellationToken cancellationToken)
    {
        var validation = await new RawMeasurementBatchValidator().ValidateAsync(batch, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var deviceId = CurrentDeviceId;
        var entities = batch.Select(b => RawMeasurement.New(
            b.Time.ToUniversalTime(), b.EmissionSourceId, b.PollutantId, deviceId,
            b.UnitId, b.RawValue, b.Quality));

        var inserted = await measurementWriter.WriteBatchAsync(entities, cancellationToken);
        return Accepted(new IngestBatchResult(inserted));
    }

    [HttpPost("process-parameters")]
    [ProducesOkApiResponseType<IngestBatchResult>]
    public async Task<IActionResult> IngestProcessParameters(
        [FromBody] IReadOnlyList<RawProcessParameterIngestDto> batch,
        CancellationToken cancellationToken)
    {
        var validation = await new RawProcessParameterBatchValidator().ValidateAsync(batch, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var deviceId = CurrentDeviceId;
        var entities = batch.Select(b => RawProcessParameter.New(
            b.Time.ToUniversalTime(), b.EmissionSourceId, deviceId,
            b.ParameterType, b.Value, b.UnitId, b.Quality));

        var inserted = await parameterWriter.WriteBatchAsync(entities, cancellationToken);
        return Accepted(new IngestBatchResult(inserted));
    }
}
