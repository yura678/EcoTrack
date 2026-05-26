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
    : InstallationException(installationId, "Installation not found.");

public class IedCategoryNotFoundException(
    Guid installationId,
    Guid iedCategoryId)
    : InstallationException(installationId, "Industrial category not found.")
{
    public Guid IedCategoryId { get; } = iedCategoryId;
}

public class SiteNotFoundException(
    Guid installationId,
    Guid siteId)
    : InstallationException(installationId, "Site not found.")
{
    public Guid SiteId { get; } = siteId;
}

public class InstallationHasDependenciesException(
    Guid installationId)
    : InstallationException(installationId,
        "Installation has related data (emission sources, devices) and cannot be deleted.");

public class UnhandledInstallationException(Guid installationId, Exception? innerException = null)
    : InstallationException(installationId, "Unexpected error occurred.", innerException);
