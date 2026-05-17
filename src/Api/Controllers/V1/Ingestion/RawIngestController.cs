using Api.Attributes;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Features.RawIngest.Commands.IngestMeasurements;
using Application.Features.RawIngest.Commands.IngestProcessParameters;
using Asp.Versioning;
using Infrastructure.Identity.Ingestion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Ingestion;

[ApiVersion("1")]
[Authorize(AuthenticationSchemes = DeviceHmacAuthDefaults.Scheme)]
[Route("api/v{version:apiVersion}/ingest")]
[ApiController]
public class RawIngestController(ISender sender) : ControllerBase
{
    private Guid CurrentDeviceId =>
        Guid.Parse(User.FindFirst("DeviceId")!.Value);

    [HttpPost("measurements")]
    [ProducesOkApiResponseType<IngestBatchResult>]
    public async Task<IActionResult> IngestMeasurements(
        [FromBody] List<RawMeasurementIngestDto> batch,
        CancellationToken cancellationToken)
    {
        var command = new IngestMeasurementsCommand
        {
            DeviceId = CurrentDeviceId,
            Batch = batch.Select(b => new IngestMeasurementInput(
                b.Time, b.EmissionSourceId, b.PollutantId, b.UnitId, b.RawValue, b.Quality)).ToList()
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match<IActionResult>(
            ok => Accepted(new IngestBatchResult(ok.InsertedCount)),
            err => err.ToObjectResult());
    }

    [HttpPost("process-parameters")]
    [ProducesOkApiResponseType<IngestBatchResult>]
    public async Task<IActionResult> IngestProcessParameters(
        [FromBody] List<RawProcessParameterIngestDto> batch,
        CancellationToken cancellationToken)
    {
        var command = new IngestProcessParametersCommand
        {
            DeviceId = CurrentDeviceId,
            Batch = batch.Select(b => new IngestProcessParameterInput(
                b.Time, b.EmissionSourceId, b.ParameterType, b.Value, b.UnitId, b.Quality)).ToList()
        };

        var result = await sender.Send(command, cancellationToken);
        return result.Match<IActionResult>(
            ok => Accepted(new IngestBatchResult(ok.InsertedCount)),
            err => err.ToObjectResult());
    }
}
