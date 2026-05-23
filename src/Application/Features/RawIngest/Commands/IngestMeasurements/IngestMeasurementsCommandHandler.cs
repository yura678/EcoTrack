using Application.Common.Interfaces.Ingestion;
using Application.Common.Interfaces.Queries.Emissions;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.RawIngest.Exceptions;
using Domain.Entities.Monitoring;
using Domain.Services;
using LanguageExt;
using MediatR;

namespace Application.Features.RawIngest.Commands.IngestMeasurements;

public class IngestMeasurementsCommandHandler(
    IRawMeasurementWriter writer,
    IDevicePollutantCapabilityQueries capabilityQueries,
    IPollutantQueries pollutantQueries,
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

            // Canonical-unit gate: load each pollutant's CanonicalUnitId + MolarMass plus every
            // MeasureUnit involved (batch units, canonical units, capability range units) so the
            // per-row convertibility check is pure in-memory and produces a single batch-wide
            // pass/fail decision.
            var pollutants = (await pollutantQueries.GetByIdsAsync(requestedPollutants, cancellationToken))
                .ToDictionary(p => p.Id);
            var capabilityRanges = await capabilityQueries.GetCapabilityRangesForDeviceAsync(
                request.DeviceId, requestedPollutants, cancellationToken);
            var unitIds = request.Batch.Select(b => b.UnitId)
                .Concat(capabilityRanges.Values.Select(c => c.RangeUnitId))
                .Concat(pollutants.Values.Select(p => p.CanonicalUnitId))
                .Distinct()
                .ToList();
            var units = (await unitQueries.GetByIdsAsync(unitIds, cancellationToken))
                .ToDictionary(u => u.Id);

            var failures = new List<UnconvertibleUnitFailure>();
            var qualities = new Quality[request.Batch.Count];
            for (var i = 0; i < request.Batch.Count; i++)
            {
                qualities[i] = EvaluateRow(
                    rowIndex: i,
                    input: request.Batch[i],
                    pollutants, capabilityRanges, units, failures);
            }

            if (failures.Count > 0)
            {
                return new UnconvertibleUnitsException(request.DeviceId, failures);
            }

            var entities = request.Batch.Select((b, i) => RawMeasurement.New(
                b.Time.ToUniversalTime(), b.EmissionSourceId, b.PollutantId, request.DeviceId,
                b.UnitId, b.RawValue, qualities[i]));
            var inserted = await writer.WriteBatchAsync(entities, cancellationToken);
            return new IngestMeasurementsResult(inserted);
        }
        catch (Exception ex)
        {
            return new UnhandledRawIngestException(ex);
        }
    }

    /// <summary>
    /// Returns the effective Quality for one row and accumulates any conversion failures into
    /// <paramref name="failures"/>. Quality already non-Valid (Calibration/Maintenance/etc.) is
    /// left alone — those carry a more specific reason than "out of range". A row that fails to
    /// convert is recorded as a failure; its Quality value is incidental because the whole batch
    /// will be rejected upstream.
    /// </summary>
    private static Quality EvaluateRow(
        int rowIndex,
        IngestMeasurementInput input,
        IReadOnlyDictionary<Guid, Domain.Entities.EmissionSources.Pollutant> pollutants,
        IReadOnlyDictionary<Guid, DeviceCapabilityRange> capabilityRanges,
        IReadOnlyDictionary<Guid, MeasureUnit> units,
        List<UnconvertibleUnitFailure> failures)
    {
        // Defensive lookups. Pollutant/CanonicalUnit lookups are DB-prevented by FK Restrict on
        // DevicePollutantCapability and Pollutant.CanonicalUnit, so missing here would mean a
        // racy schema state. The measurement-unit case is real: the device can ship any GUID.
        // Either way, surface as a failure so the batch returns 422 with diagnostics instead of
        // writing un-convertible data to raw_measurement.
        if (!pollutants.TryGetValue(input.PollutantId, out var pollutant))
        {
            failures.Add(new UnconvertibleUnitFailure(
                rowIndex, input.PollutantId,
                input.UnitId, "(unknown)", Guid.Empty, "(unknown)",
                $"Pollutant {input.PollutantId} not found — capability references a pollutant " +
                "that no longer exists."));
            return input.Quality;
        }
        if (!units.TryGetValue(pollutant.CanonicalUnitId, out var canonicalUnit))
        {
            failures.Add(new UnconvertibleUnitFailure(
                rowIndex, pollutant.Id,
                input.UnitId, "(unknown)",
                pollutant.CanonicalUnitId, "(unknown)",
                $"Pollutant {pollutant.Id}'s canonical unit {pollutant.CanonicalUnitId} is not " +
                "registered in the MeasureUnit table."));
            return input.Quality;
        }
        if (!units.TryGetValue(input.UnitId, out var measurementUnit))
        {
            failures.Add(new UnconvertibleUnitFailure(
                rowIndex, pollutant.Id,
                input.UnitId, "(unknown)",
                canonicalUnit.Id, canonicalUnit.Symbol,
                $"Unit {input.UnitId} shipped by the device is not registered in the " +
                "MeasureUnit table; register it or fix the device configuration."));
            return input.Quality;
        }

        // TryToCanonical instead of throwing — a misconfigured device can ship a 1000-row batch
        // where every entry fails; each thrown exception captures a stack trace and would melt
        // a CPU core for nothing.
        if (!UnitConverter.TryToCanonical(
                input.RawValue, measurementUnit, canonicalUnit, pollutant.MolarMass,
                out var valueCanonical, out var readingError))
        {
            failures.Add(new UnconvertibleUnitFailure(
                rowIndex, pollutant.Id,
                measurementUnit.Id, measurementUnit.Symbol,
                canonicalUnit.Id, canonicalUnit.Symbol,
                readingError!));
            return input.Quality;
        }

        if (input.Quality != Quality.Valid) return input.Quality;
        if (!capabilityRanges.TryGetValue(input.PollutantId, out var range)) return input.Quality;
        if (!units.TryGetValue(range.RangeUnitId, out var rangeUnit)) return input.Quality;

        if (!UnitConverter.TryToCanonical(
                range.RangeMin, rangeUnit, canonicalUnit, pollutant.MolarMass,
                out var minCanonical, out var minError)
            || !UnitConverter.TryToCanonical(
                range.RangeMax, rangeUnit, canonicalUnit, pollutant.MolarMass,
                out var maxCanonical, out _))
        {
            // Operator-declared capability range can't be reconciled with the pollutant's
            // canonical — report under the offending row so the UI can flag both the device
            // and the misconfigured capability.
            failures.Add(new UnconvertibleUnitFailure(
                rowIndex, pollutant.Id,
                rangeUnit.Id, rangeUnit.Symbol,
                canonicalUnit.Id, canonicalUnit.Symbol,
                $"Capability range unit cannot be converted to canonical: {minError}"));
            return input.Quality;
        }

        var outOfRange = valueCanonical < minCanonical || valueCanonical > maxCanonical;
        return outOfRange ? Quality.Invalid : input.Quality;
    }
}
