using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record MonitoringPlanQueryDto(
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

public record CreateMonitoringPlanDto(
    string Version,
    string? Notes,
    IReadOnlyList<CreateMonitoringRequirementDto> MonitoringRequirements);

public record UpdateMonitoringPlanDto(
    string Version,
    string? Notes,
    IReadOnlyList<UpdateMonitoringRequirementDto> MonitoringRequirements);

public record CreateMonitoringRequirementDto(
    Guid EmissionSourceId,
    Guid PollutantId,
    Frequency Frequency);

public record UpdateMonitoringRequirementDto(
    Guid? Id,
    Guid PollutantId,
    Guid EmissionSourceId,
    Frequency Frequency);

public record MonitoringPlanDto(
    Guid Id,
    Guid InstallationId,
    InstallationDto? Installation,
    string Version,
    MonitoringPlanStatus Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<MonitoringRequirementDto>? MonitoringRequirements)
{
    public static MonitoringPlanDto FromDomainModel(MonitoringPlan monitoringPlan)
    {
        return new MonitoringPlanDto(
            monitoringPlan.Id,
            monitoringPlan.InstallationId,
            monitoringPlan.Installation is not null
                ? InstallationDto.FromDomainModel(monitoringPlan.Installation)
                : null,
            monitoringPlan.Version,
            monitoringPlan.Status,
            monitoringPlan.Notes,
            monitoringPlan.CreatedAt,
            monitoringPlan.UpdatedAt,
            monitoringPlan.Requirements?.Select(MonitoringRequirementDto.FromDomainModel).ToList()
        );
    }
}

public record MonitoringRequirementDto(
    Guid Id,
    Guid MonitoringPlanId,
    Guid EmissionSourceId,
    EmissionSourceDto? EmissionSource,
    Guid PollutantId,
    PollutantDto? Pollutant,
    Frequency Frequency)
{
    public static MonitoringRequirementDto FromDomainModel(MonitoringRequirement monitoringRequirement)
    {
        return new MonitoringRequirementDto(
            monitoringRequirement.Id,
            monitoringRequirement.MonitoringPlanId,
            monitoringRequirement.EmissionSourceId,
            monitoringRequirement.EmissionSource is not null
                ? EmissionSourceDto.FromDomainModel(monitoringRequirement.EmissionSource)
                : null,
            monitoringRequirement.PollutantId,
            monitoringRequirement.Pollutant is not null
                ? PollutantDto.FromDomainModel(monitoringRequirement.Pollutant)
                : null,
            monitoringRequirement.Frequency
        );
    }
}