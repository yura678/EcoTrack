using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MonitoringRequirementsData
{
    public static MonitoringRequirement FirstTestRequirement(
        Guid planId,
        Guid emissionSourceId,
        Guid pollutantId)
        => MonitoringRequirement.New(
            id: Guid.NewGuid(),
            monitoringPlanId: planId,
            emissionSourceId: emissionSourceId,
            pollutantId: pollutantId,
            frequency: Frequency.Hourly);

    public static MonitoringRequirement SecondTestRequirement(
        Guid planId,
        Guid emissionSourceId,
        Guid pollutantId)
        => MonitoringRequirement.New(
            id: Guid.NewGuid(),
            monitoringPlanId: planId,
            emissionSourceId: emissionSourceId,
            pollutantId: pollutantId,
            frequency: Frequency.Daily
        );
}