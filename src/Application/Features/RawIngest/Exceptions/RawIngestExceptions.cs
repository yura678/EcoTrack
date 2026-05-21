namespace Application.Features.RawIngest.Exceptions;

public abstract class RawIngestException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class UnconfiguredDevicePollutantsException(
    Guid deviceId,
    IReadOnlyCollection<Guid> unconfiguredPollutantIds)
    : RawIngestException(
        $"Device '{deviceId}' has no DevicePollutantCapability for pollutant(s): " +
        $"{string.Join(", ", unconfiguredPollutantIds)}.")
{
    public Guid DeviceId { get; } = deviceId;
    public IReadOnlyCollection<Guid> UnconfiguredPollutantIds { get; } = unconfiguredPollutantIds;
}

/// <summary>
/// One batch entry uses a unit that cannot be converted to its pollutant's canonical unit, or
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
        $"Device '{deviceId}' shipped {failures.Count} reading(s) whose unit cannot be reconciled " +
        $"with the pollutant's canonical unit. First failure: row {failures.First().RowIndex} " +
        $"({failures.First().FromUnitSymbol} → {failures.First().CanonicalUnitSymbol}): " +
        $"{failures.First().Reason}.")
{
    public Guid DeviceId { get; } = deviceId;
    public IReadOnlyCollection<UnconvertibleUnitFailure> Failures { get; } = failures;
}

public class UnhandledRawIngestException(Exception innerException)
    : RawIngestException("Unexpected error occurred while ingesting raw data.", innerException);
