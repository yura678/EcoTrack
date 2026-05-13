using Domain.Entities.Monitoring;

namespace Application.Features.ExceedanceEvents.Exceptions;

public abstract class ExceedanceEventException(
    Guid id,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid Id { get; } = id;
}

public class ExceedanceEventNotFoundException(Guid exceedanceEventId)
    : ExceedanceEventException(exceedanceEventId, $"Exceedance event with ID '{exceedanceEventId}' was not found.");

public class UnhandledExceedanceEventException(Guid id, Exception innerException)
    : ExceedanceEventException(id,
        $"Unexpected error occurred while processing exceedance event.", innerException);
