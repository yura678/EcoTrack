namespace Application.Features.EmissionSources.Exceptions;

public abstract class EmissionSourceException(
    Guid emissionSourceId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid EmissionSourceId { get; } = emissionSourceId;
}

public class EmissionSourceCodeAlreadyExistsException(Guid emissionSourceId, string code)
    : EmissionSourceException(emissionSourceId,
        $"An emission source with code '{code}' already exists.")
{
    public string Code { get; } = code;
}

public class EmissionSourceNotFoundException(Guid emissionSourceId)
    : EmissionSourceException(emissionSourceId, "Emission source not found.");

public sealed class EmissionSourceTypeMismatchException(
    Guid id,
    Type expectedType,
    Type actualType)
    : EmissionSourceException(id,
        $"Emission source type mismatch: expected '{expectedType.Name}', got '{actualType.Name}'.")
{
    public Type ExpectedType { get; } = expectedType;
    public Type ActualType { get; } = actualType;
}

public class InstallationNotFoundException(
    Guid emissionSourceId,
    Guid installationId)
    : EmissionSourceException(emissionSourceId, "Installation not found.")
{
    public Guid InstallationId { get; } = installationId;
}

public class EmissionSourceHasDependenciesException(
    Guid emissionSourceId)
    : EmissionSourceException(emissionSourceId,
        "Emission source has related data (measurements, devices) and cannot be deleted.");

public class UnhandledEmissionSourceException(
    Guid emissionSourceId,
    Exception? innerException = null)
    : EmissionSourceException(emissionSourceId, "Unexpected error occurred.", innerException);
