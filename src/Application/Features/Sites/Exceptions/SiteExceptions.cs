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
    : SiteException(siteId, $"Site with ID '{siteId}' was not found.");

public class EnterpriseNotFoundException(Guid siteId, Guid enterpriseId)
    : SiteException(siteId, $"Enterprise with ID '{enterpriseId}' was not found.");

public class SiteHasDependenciesException(
    Guid siteId)
    : SiteException(siteId, $"Site with ID '{siteId}' has dependencies and cannot be deleted.");

public class UnhandledSiteException(Guid siteId, Exception? innerException = null)
    : SiteException(siteId, "Unexpected error occurred.", innerException);