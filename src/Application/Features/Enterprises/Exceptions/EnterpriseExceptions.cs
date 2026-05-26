namespace Application.Features.Enterprises.Exceptions;

public abstract class EnterpriseException(
    Guid enterpriseId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid EnterpriseId { get; } = enterpriseId;
}

public class EnterpriseEdrpouAlreadyExistsException(Guid enterpriseId, string edrpou)
    : EnterpriseException(enterpriseId,
        $"An enterprise with EDRPOU '{edrpou}' already exists.")
{
    public string Edrpou { get; } = edrpou;
}

public class EnterpriseNotFoundException(Guid enterpriseId)
    : EnterpriseException(enterpriseId, "Enterprise not found.");

public class SectorNotFoundException(Guid enterpriseId, Guid sectorId)
    : EnterpriseException(enterpriseId, "Sector not found.")
{
    public Guid SectorId { get; } = sectorId;
}

public class EnterpriseHasDependenciesException(
    Guid enterpriseId)
    : EnterpriseException(enterpriseId,
        "Enterprise has related data (sites, users, etc.) and cannot be deleted.");

public class UnhandledEnterpriseException(Guid enterpriseId, Exception? innerException = null)
    : EnterpriseException(enterpriseId, "Unexpected error occurred.", innerException);

public class EnterpriseAlreadyActiveException(Guid enterpriseId)
    : EnterpriseException(enterpriseId,
        "Enterprise is already active; nothing to approve.");

public class EnterpriseInvalidStateForApprovalException(Guid enterpriseId, string currentStatus)
    : EnterpriseException(enterpriseId,
        $"Enterprise status is '{currentStatus}' and cannot be approved.")
{
    public string CurrentStatus { get; } = currentStatus;
}

public class EnterpriseInvalidStateForRejectionException(Guid enterpriseId, string currentStatus)
    : EnterpriseException(enterpriseId,
        $"Enterprise status is '{currentStatus}' and cannot be rejected.")
{
    public string CurrentStatus { get; } = currentStatus;
}

public class EnterpriseRejectionReasonRequiredException(Guid enterpriseId)
    : EnterpriseException(enterpriseId, "Rejection reason is required.");
