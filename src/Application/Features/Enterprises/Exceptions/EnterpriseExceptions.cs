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
        $"Enterprise already exists with edrpou '{edrpou}' under ID '{enterpriseId}'.");

public class EnterpriseNotFoundException(Guid enterpriseId)
    : EnterpriseException(enterpriseId, $"Enterprise with ID '{enterpriseId}' was not found.");

public class SectorNotFoundException(Guid enterpriseId, Guid sectorId)
    : EnterpriseException(enterpriseId, $"Sector with ID '{sectorId}' was not found.");

public class EnterpriseHasDependenciesException(
    Guid enterpriseId)
    : EnterpriseException(enterpriseId, $"Enterprise with ID '{enterpriseId}' has dependencies and cannot be deleted.");

public class UnhandledEnterpriseException(Guid enterpriseId, Exception? innerException = null)
    : EnterpriseException(enterpriseId, "Unexpected error occurred.", innerException);