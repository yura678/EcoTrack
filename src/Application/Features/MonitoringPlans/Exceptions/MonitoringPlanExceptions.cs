using Domain.Entities.Monitoring;

namespace Application.Features.MonitoringPlans.Exceptions;

public abstract class MonitoringPlanException(
    Guid monitoringPlanId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid MonitoringPlanId { get; } = monitoringPlanId;
}

public class MonitoringPlanNotFoundException(
    Guid monitoringPlanId)
    : MonitoringPlanException(monitoringPlanId, $"Monitoring plan with ID '{monitoringPlanId}' was not found.");

public class MonitoringRequirementNotFoundException(
    Guid monitoringPlanId,
    Guid monitoringRequirementId)
    : MonitoringPlanException(monitoringPlanId,
        $"Monitoring requirement with ID '{monitoringRequirementId}' was not found.");

public class MonitoringPlanInvalidStatusException(
    Guid monitoringPlanId,
    MonitoringPlanStatus status,
    string massage) : MonitoringPlanException(monitoringPlanId,
    $"Status of monitoring plan '{monitoringPlanId}' is {status}. {massage}");

public class ActiveMonitoringPlanAlreadyExistsException(
    Guid monitoringPlanId)
    : MonitoringPlanException(
        monitoringPlanId,
        $"Active monitoring plan already exists with ID '{monitoringPlanId}' for this installation.");

public class InstallationNotFoundException(
    Guid monitoringPlanId,
    Guid installationId)
    : MonitoringPlanException(monitoringPlanId, $"Installation with ID '{installationId}' was not found.");

public class EmissionSourceNotFoundException(
    Guid monitoringPlanId,
    IReadOnlyList<Guid> emissionSourceIds)
    : MonitoringPlanException(monitoringPlanId,
        $"Emissions source with IDs '{string.Join(", ", emissionSourceIds)}' were not found.");

public class PollutantNotFoundException(
    Guid monitoringPlanId,
    IReadOnlyList<Guid> pollutantIds)
    : MonitoringPlanException(monitoringPlanId,
        $"Pollutants with IDs '{string.Join(", ", pollutantIds)}' were not found.");

public class UnhandledMonitoringPlanException(
    Guid monitoringPlanId,
    Exception? innerException = null)
    : MonitoringPlanException(monitoringPlanId, "Unexpected error occurred.", innerException);