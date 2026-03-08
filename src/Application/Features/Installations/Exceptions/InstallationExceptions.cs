namespace Application.Features.Installations.Exceptions;

public abstract class InstallationException(
    Guid installationId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid InstallationId { get; } = installationId;
}

public class InstallationNotFoundException(Guid installationId)
    : InstallationException(installationId, $"Installation with ID '{installationId}' was not found.");

public class IedCategoryNotFoundException(
    Guid installationId,
    Guid iedCategoryId)
    : InstallationException(installationId, $"IedCategory with ID '{iedCategoryId}' was not found.");

public class SiteNotFoundException(
    Guid installationId,
    Guid siteId)
    : InstallationException(installationId, $"Site with ID '{siteId}' was not found.");

public class InstallationHasDependenciesException(
    Guid installationId)
    : InstallationException(installationId,
        $"Installation with ID '{installationId}' has dependencies and cannot be deleted.");

public class UnhandledInstallationException(Guid installationId, Exception? innerException = null)
    : InstallationException(installationId, "Unexpected error occurred.", innerException);