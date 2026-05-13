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
    : ComplianceEventException(id, $"Compliance event with ID '{id}' was not found.");

public class UnhandledComplianceEventException(Guid id, Exception innerException)
    : ComplianceEventException(id,
        $"Unexpected error occurred while processing compliance event.", innerException);
