using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Application.Features.Permits.Exceptions;

public abstract class PermitException(
    Guid permitId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid PermitId { get; } = permitId;
}

public class PermitNotFoundException(
    Guid permitId)
    : PermitException(permitId, "Permit not found.");

public class InstallationNotFoundException(
    Guid permitId,
    Guid installationId)
    : PermitException(permitId, "Installation not found.")
{
    public Guid InstallationId { get; } = installationId;
}

public class InvalidEmissionLimitDateRangeException(
    Guid permitId,
    string message) : PermitException(permitId, message);

public class EmissionLimitNotFoundException(
    Guid permitId,
    Guid emissionLimitId)
    : PermitException(permitId, "Emission limit not found.")
{
    public Guid EmissionLimitId { get; } = emissionLimitId;
}

public class PermitNumberAlreadyExistsException(
    Guid permitId,
    string number)
    : PermitException(permitId, $"A permit with number '{number}' already exists.")
{
    public string Number { get; } = number;
}

public class PermitInvalidStatusException(
    Guid permitId,
    PermitStatus status,
    string message) : PermitException(permitId,
    $"Permit status is {status}. {message}")
{
    public PermitStatus Status { get; } = status;
}

public class MeasureUnitNotFoundException(
    Guid permitId,
    IReadOnlyList<Guid> unitIds)
    : PermitException(permitId,
        $"{unitIds.Count} measurement unit(s) were not found.")
{
    public IReadOnlyList<Guid> UnitIds { get; } = unitIds;
}

public class EmissionSourceNotFoundException(
    Guid permitId,
    IReadOnlyList<Guid> emissionSourceIds)
    : PermitException(permitId,
        $"{emissionSourceIds.Count} emission source(s) were not found.")
{
    public IReadOnlyList<Guid> EmissionSourceIds { get; } = emissionSourceIds;
}

public class PollutantNotFoundException(
    Guid permitId,
    IReadOnlyList<Guid> pollutantIds)
    : PermitException(permitId,
        $"{pollutantIds.Count} pollutant(s) were not found.")
{
    public IReadOnlyList<Guid> PollutantIds { get; } = pollutantIds;
}

public class ActivePermitAlreadyExistsException(
    Guid permitId)
    : PermitException(
        permitId,
        "An active permit already exists for this installation.");

/// <summary>
/// Operator tried to activate a permit on an installation that is itself Decommissioned.
/// Activating a permit on a decommissioned installation creates a phantom regulatory context
/// — the detector would generate LimitExceedance events for devices that don't legitimately
/// operate, polluting the audit trail.
/// </summary>
public class CannotActivatePermitOnDecommissionedInstallationException(
    Guid permitId,
    Guid installationId)
    : PermitException(permitId,
        "Permit cannot be activated while the installation is shut down. " +
        "Reactivate the installation first.")
{
    public Guid InstallationId { get; } = installationId;
}

public class IncompatibleLimitUnitDimensionException(
    Guid permitId,
    LimitType limitType,
    MeasureUnitDimension actualDimension,
    IReadOnlyCollection<MeasureUnitDimension> allowedDimensions)
    : PermitException(permitId,
        $"Limit of type '{limitType}' requires a unit of type " +
        $"[{string.Join(", ", allowedDimensions)}], but the selected unit is '{actualDimension}'.")
{
    public LimitType LimitType { get; } = limitType;
    public MeasureUnitDimension ActualDimension { get; } = actualDimension;
    public IReadOnlyCollection<MeasureUnitDimension> AllowedDimensions { get; } = allowedDimensions;
}

public class UnhandledPermitException(
    Guid permitId,
    Exception? innerException = null)
    : PermitException(permitId, "Unexpected error occurred.", innerException);
