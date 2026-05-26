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
        $"A pollutant with code '{code}' already exists.")
{
    public string Code { get; } = code;
}

public class PollutantNameAlreadyExistsException(Guid pollutantId, string name)
    : PollutantException(pollutantId,
        $"A pollutant with name '{name}' already exists.")
{
    public string Name { get; } = name;
}

public class PollutantNotFoundException(Guid pollutantId)
    : PollutantException(pollutantId, "Pollutant not found.");

public class PollutantHasDependenciesException(Guid pollutantId)
    : PollutantException(pollutantId,
        "Pollutant has related data (measurements, limits) and cannot be deleted.");

public class UnhandledPollutantException(Guid pollutantId, Exception? innerException = null)
    : PollutantException(pollutantId, "Unexpected error occurred.", innerException);
