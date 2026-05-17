using Application.Common.Interfaces.Ingestion;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.RawIngest.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.RawIngest.Commands.IngestMeasurements;

public class IngestMeasurementsCommandHandler(
    IRawMeasurementWriter writer,
    IDevicePollutantCapabilityQueries capabilityQueries)
    : IRequestHandler<IngestMeasurementsCommand,
        Either<RawIngestException, IngestMeasurementsResult>>
{
    public async Task<Either<RawIngestException, IngestMeasurementsResult>> Handle(
        IngestMeasurementsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Capability gate: every pollutant in the batch must be registered as a
            // DevicePollutantCapability for this device. Protects against misconfigured
            // devices flooding raw_measurement with pollutants nobody promised the sensor
            // can read.
            var requestedPollutants = request.Batch
                .Select(b => b.PollutantId)
                .Distinct()
                .ToArray();
            var configured = await capabilityQueries.GetConfiguredPollutantsForDeviceAsync(
                request.DeviceId, requestedPollutants, cancellationToken);
            var missing = requestedPollutants.Where(p => !configured.Contains(p)).ToList();
            if (missing.Count > 0)
            {
                return new UnconfiguredDevicePollutantsException(request.DeviceId, missing);
            }

            var entities = request.Batch.Select(b => RawMeasurement.New(
                b.Time.ToUniversalTime(), b.EmissionSourceId, b.PollutantId, request.DeviceId,
                b.UnitId, b.RawValue, b.Quality));
            var inserted = await writer.WriteBatchAsync(entities, cancellationToken);
            return new IngestMeasurementsResult(inserted);
        }
        catch (Exception ex)
        {
            return new UnhandledRawIngestException(ex);
        }
    }
}
