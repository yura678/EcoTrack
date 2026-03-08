namespace Application.Features.Pollutants.Exceptions;

public abstract class PollutantException(
    Guid pollutantId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid PollutantId { get; } = pollutantId;
}

public class PollutantCodeAlreadyExistsException(Guid pollutantId, string code)
    : PollutantException(pollutantId,
        $"Pollutant already exists with code '{code}' under ID '{pollutantId}'.");


public class PollutantNameAlreadyExistsException(Guid pollutantId, string name)
    : PollutantException(pollutantId,
        $"Pollutant already exists with name '{name}' under ID '{pollutantId}'.");

public class PollutantNotFoundException(Guid pollutantId)
    : PollutantException(pollutantId, $"Pollutant with ID '{pollutantId}' was not found.");

public class PollutantHasDependenciesException(Guid pollutantId)
    : PollutantException(pollutantId, $"Pollutant with ID '{pollutantId}' has dependencies and cannot be deleted.");

public class UnhandledPollutantException(Guid pollutantId, Exception? innerException = null)
    : PollutantException(pollutantId, "Unexpected error occurred.", innerException);