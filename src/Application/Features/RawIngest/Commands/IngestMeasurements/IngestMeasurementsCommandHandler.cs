using Application.Common.Interfaces.Ingestion;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.RawIngest.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.RawIngest.Commands.IngestMeasurements;

public class IngestMeasurementsCommandHandler(
    IRawMeasurementWriter writer,
    IDevicePollutantCapabilityQueries capabilityQueries,
    IMeasureUnitQueries unitQueries)
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

            // Range check: load capability ranges + the units involved so we can compare in
            // base units. Out-of-range values get their Quality forced to Invalid so downstream
            // detection ignores them. Quality already non-Valid (Calibration/Maintenance/etc.)
            // is left alone — those carry a more specific reason than "out of range".
            var capabilityRanges = await capabilityQueries.GetCapabilityRangesForDeviceAsync(
                request.DeviceId, requestedPollutants, cancellationToken);
            var unitIds = request.Batch.Select(b => b.UnitId)
                .Concat(capabilityRanges.Values.Select(c => c.RangeUnitId))
                .Distinct()
                .ToList();
            var units = (await unitQueries.GetByIdsAsync(unitIds, cancellationToken))
                .ToDictionary(u => u.Id);

            var entities = request.Batch.Select(b =>
            {
                var effectiveQuality = DecideQuality(b, capabilityRanges, units);
                return RawMeasurement.New(
                    b.Time.ToUniversalTime(), b.EmissionSourceId, b.PollutantId, request.DeviceId,
                    b.UnitId, b.RawValue, effectiveQuality);
            });
            var inserted = await writer.WriteBatchAsync(entities, cancellationToken);
            return new IngestMeasurementsResult(inserted);
        }
        catch (Exception ex)
        {
            return new UnhandledRawIngestException(ex);
        }
    }

    private static Quality DecideQuality(
        IngestMeasurementInput input,
        IReadOnlyDictionary<Guid, DeviceCapabilityRange> capabilityRanges,
        IReadOnlyDictionary<Guid, MeasureUnit> units)
    {
        if (input.Quality != Quality.Valid) return input.Quality;
        if (!capabilityRanges.TryGetValue(input.PollutantId, out var range)) return input.Quality;
        if (!units.TryGetValue(input.UnitId, out var measurementUnit)) return input.Quality;
        if (!units.TryGetValue(range.RangeUnitId, out var rangeUnit)) return input.Quality;
        if (measurementUnit.Dimension != rangeUnit.Dimension) return input.Quality;

        var valueBase = input.RawValue * measurementUnit.ToBaseFactor;
        var minBase = range.RangeMin * rangeUnit.ToBaseFactor;
        var maxBase = range.RangeMax * rangeUnit.ToBaseFactor;
        var outOfRange = valueBase < minBase || valueBase > maxBase;
        return outOfRange ? Quality.Invalid : input.Quality;
    }
}
