using Domain.Entities.Monitoring;

namespace Application.Features.ComplianceEvents.Exceptions;

public abstract class ComplianceEventException(
    Guid id,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid Id { get; } = id;
}

public class ComplianceEventNotFoundException(Guid id)
    : ComplianceEventException(id, "Compliance event not found.");

public class ComplianceEventInvalidStatusException(
    Guid id, ComplianceEventStatus currentStatus, string message)
    : ComplianceEventException(id,
        $"Compliance event is {currentStatus}. {message}")
{
    public ComplianceEventStatus CurrentStatus { get; } = currentStatus;
}

public class UnhandledComplianceEventException(Guid id, Exception innerException)
    : ComplianceEventException(id,
        "Unexpected error occurred while processing compliance event.", innerException);
