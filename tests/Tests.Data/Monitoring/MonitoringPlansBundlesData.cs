using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MonitoringPlansBundlesData
{
    public static (MonitoringPlan Plan, MonitoringRequirement[] Requirements)
        ActivePlanBundle(
            Guid installationId,
            Guid source1,
            Guid source2,
            Guid pollutant1,
            Guid pollutant2)
    {
        var planId = Guid.NewGuid();

        var req1 = MonitoringRequirementsData.FirstTestRequirement(planId, source1, pollutant1);
        var req2 = MonitoringRequirementsData.SecondTestRequirement(planId, source2, pollutant2);

        var plan = MonitoringPlansData.FirstActiveTestPlan(
            planId, installationId, new[] { req1, req2 });

        return (plan, new[] { req1, req2 });
    }

    public static (MonitoringPlan Plan, MonitoringRequirement[] Requirements)
        DraftPlanBundle(
            Guid installationId,
            Guid source1,
            Guid source2,
            Guid pollutant1,
            Guid pollutant2)
    {
        var planId = Guid.NewGuid();

        var req1 = MonitoringRequirementsData.FirstTestRequirement(planId, source1, pollutant1);
        var req2 = MonitoringRequirementsData.SecondTestRequirement(planId, source2, pollutant2);

        var plan = MonitoringPlansData.SecondDraftTestPlan(
            planId, installationId, new[] { req1, req2 });

        return (plan, new[] { req1, req2 });
    }

    public static (MonitoringPlan Plan, MonitoringRequirement[] Requirements)
        ArchivedPlanBundle(
            Guid installationId,
            Guid source1,
            Guid source2,
            Guid pollutant1,
            Guid pollutant2)
    {
        var planId = Guid.NewGuid();

        var req1 = MonitoringRequirementsData.FirstTestRequirement(planId, source1, pollutant1);
        var req2 = MonitoringRequirementsData.SecondTestRequirement(planId, source2, pollutant2);

        var plan = MonitoringPlansData.SecondArchiveTestPlan(
            planId, installationId, new[] { req1, req2 });

        return (plan, new[] { req1, req2 });
    }
}