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
        $"Emission already exists with code '{code}' under ID '{emissionSourceId}'.");

public class EmissionSourceNotFoundException(Guid emissionSourceId)
    : EmissionSourceException(emissionSourceId, $"Emission source with ID '{emissionSourceId}' was not found.");

public sealed class EmissionSourceTypeMismatchException(
    Guid id,
    Type expectedType,
    Type actualType)
    : EmissionSourceException(id,
        $"EmissionSource '{id}' has wrong type. Expected '{expectedType.Name}', but found '{actualType.Name}'.");

public class InstallationNotFoundException(
    Guid emissionSourceId,
    Guid installationId)
    : EmissionSourceException(emissionSourceId, $"Installation with ID '{installationId}' was not found.");

public class EmissionSourceHasDependenciesException(
    Guid emissionSourceId)
    : EmissionSourceException(emissionSourceId,
        $"Emission source with ID '{emissionSourceId}' has dependencies and cannot be deleted.");

public class UnhandledEmissionSourceException(
    Guid emissionSourceId,
    Exception? innerException = null)
    : EmissionSourceException(emissionSourceId, "Unexpected error occurred.", innerException);