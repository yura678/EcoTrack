namespace Application.Features.Sites.Exceptions;

public abstract class SiteException(
    Guid siteId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid SiteId { get; } = siteId;
}

public class SiteNotFoundException(Guid siteId)
    : SiteException(siteId, "Site not found.");

public class EnterpriseNotFoundException(Guid siteId, Guid enterpriseId)
    : SiteException(siteId, "Enterprise not found.")
{
    public Guid EnterpriseId { get; } = enterpriseId;
}

public class SiteHasDependenciesException(
    Guid siteId)
    : SiteException(siteId,
        "Site has related data (installations, devices) and cannot be deleted.");

/// <summary>
/// Operator tried to archive a Site that still owns one or more Installations in
/// <c>InstallationStatus.Operating</c>. Archiving an actively-operated site would silently
/// orphan its devices (HMAC ingest would start returning 401 once the site filter hides them)
/// and lose data — so we refuse here and force the operator to shut the installations down
/// first.
/// </summary>
public class SiteHasOperatingInstallationsException(Guid siteId, int operatingCount)
    : SiteHasDependenciesException(siteId)
{
    public int OperatingCount { get; } = operatingCount;

    public override string Message =>
        $"Site cannot be archived: {OperatingCount} installation(s) are still in operation. " +
        "Shut them down first.";
}

public class UnhandledSiteException(Guid siteId, Exception? innerException = null)
    : SiteException(siteId, "Unexpected error occurred.", innerException);
