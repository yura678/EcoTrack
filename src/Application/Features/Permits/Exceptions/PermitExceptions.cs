using Domain.Entities.Enterprises;

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
    : PermitException(permitId, $"Permit with ID '{permitId}' was not found.");

public class InstallationNotFoundException(
    Guid permitId,
    Guid installationId)
    : PermitException(permitId, $"Installation with ID '{installationId}' was not found.");

public class InvalidEmissionLimitDateRangeException(
    Guid permitId,
    string message) : PermitException(permitId, message);

public class EmissionLimitNotFoundException(
    Guid permitId,
    Guid emissionLimitId)
    : PermitException(permitId, $"EmissionLimit with ID '{emissionLimitId}' was not found.");

public class PermitNumberAlreadyExistsException(
    Guid permitId,
    string number)
    : PermitException(permitId, $"Permit with number '{number}' already exists.");

public class PermitInvalidStatusException(
    Guid permitId,
    PermitStatus status,
    string message) : PermitException(permitId,
    $"Status of permit '{permitId}' is {status}. {message}");

public class MeasureUnitNotFoundException(
    Guid permitId,
    IReadOnlyList<Guid> unitIds)
    : PermitException(permitId, $"Units with IDs '{string.Join(", ", unitIds)}' were not found.");

public class EmissionSourceNotFoundException(
    Guid permitId,
    IReadOnlyList<Guid> emissionSourceIds)
    : PermitException(permitId, $"Emissions source with IDs '{string.Join(", ", emissionSourceIds)}' were not found.");

public class PollutantNotFoundException(
    Guid permitId,
    IReadOnlyList<Guid> pollutantIds)
    : PermitException(permitId, $"Pollutants with IDs '{string.Join(", ", pollutantIds)}' were not found.");

public class ActivePermitAlreadyExistsException(
    Guid permitId)
    : PermitException(
        permitId,
        $"Active permit already exists with ID '{permitId}' for this installation.");

public class UnhandledPermitException(
    Guid permitId,
    Exception? innerException = null)
    : PermitException(permitId, "Unexpected error occurred.", innerException);