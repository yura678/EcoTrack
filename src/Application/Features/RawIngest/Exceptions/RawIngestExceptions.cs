namespace Application.Features.RawIngest.Exceptions;

public abstract class RawIngestException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class UnconfiguredDevicePollutantsException(
    Guid deviceId,
    IReadOnlyCollection<Guid> unconfiguredPollutantIds)
    : RawIngestException(
        $"Device is not configured to measure {unconfiguredPollutantIds.Count} of the submitted pollutant(s). " +
        "Configure the device's pollutant list before sending data.")
{
    public Guid DeviceId { get; } = deviceId;
    public IReadOnlyCollection<Guid> UnconfiguredPollutantIds { get; } = unconfiguredPollutantIds;
}

/// <summary>
/// One batch entry uses a unit that cannot be converted to its pollutant's standard unit, or
/// the operator-declared capability range itself can't be converted. Surfaced as 422 with a
/// per-row breakdown so the UI can point the operator at exactly which entries the device
/// (or capability config) must be fixed to.
/// </summary>
public record UnconvertibleUnitFailure(
    int RowIndex,
    Guid PollutantId,
    Guid FromUnitId,
    string FromUnitSymbol,
    Guid CanonicalUnitId,
    string CanonicalUnitSymbol,
    string Reason);

public class UnconvertibleUnitsException(
    Guid deviceId,
    IReadOnlyCollection<UnconvertibleUnitFailure> failures)
    : RawIngestException(
        $"{failures.Count} reading(s) use a unit that cannot be converted to the pollutant's " +
        $"standard unit. First failure: row {failures.First().RowIndex} " +
        $"({failures.First().FromUnitSymbol} → {failures.First().CanonicalUnitSymbol}): " +
        $"{failures.First().Reason}.")
{
    public Guid DeviceId { get; } = deviceId;
    public IReadOnlyCollection<UnconvertibleUnitFailure> Failures { get; } = failures;
}

public class UnhandledRawIngestException(Exception innerException)
    : RawIngestException("Unexpected error occurred while ingesting raw data.", innerException);
