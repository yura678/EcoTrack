using Domain.Common;
using Domain.Entities.EmissionSources;

namespace Domain.Entities.Monitoring;

public class MonitoringRequirement : BaseEntity
{
    public Guid MonitoringPlanId { get; private set; }
    public MonitoringPlan? MonitoringPlan { get; private set; }

    public Guid EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }

    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }

    public Frequency Frequency { get; private set; }

    private MonitoringRequirement(Guid id, Guid monitoringPlanId,
        Guid emissionSourceId, Guid pollutantId, Frequency frequency)
    {
        Id = id;
        MonitoringPlanId = monitoringPlanId;
        EmissionSourceId = emissionSourceId;
        PollutantId = pollutantId;
        Frequency = frequency;
    }

    public static MonitoringRequirement New(Guid id,
        Guid monitoringPlanId,
        Guid emissionSourceId, Guid pollutantId, Frequency frequency)
    {
        return new MonitoringRequirement(
            id,
            monitoringPlanId,
            emissionSourceId,
            pollutantId,
            frequency);
    }

    public void UpdateDetails(Frequency frequency, Guid pollutantId,
        Guid emissionSourceId)
    {
        Frequency = frequency;
        PollutantId = pollutantId;
        EmissionSourceId = emissionSourceId;
    }
}

public enum Frequency // для MonitoringRequirement
{
    Hourly, // 1 раз на годину
    Daily // 1 раз на добу
}