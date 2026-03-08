using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class MonitoringPlansData
{
    public static MonitoringPlan FirstActiveTestPlan(
        Guid planId,
        Guid installationId,
        ICollection<MonitoringRequirement> requirements)
    {
        var plan = MonitoringPlan.New(
            id: planId,
            installationId: installationId,
            version: "v1",
            notes: "Test monitoring plan",
            requirements: requirements);

        plan.ChangeStatus(MonitoringPlanStatus.Active);
        return plan;
    }

    public static MonitoringPlan SecondDraftTestPlan(
        Guid planId,
        Guid installationId,
        ICollection<MonitoringRequirement> requirements)
    {
        var plan = MonitoringPlan.New(
            id: planId,
            installationId: installationId,
            version: "v2",
            notes: "Test monitoring plan v2",
            requirements: requirements
        );

        plan.ChangeStatus(MonitoringPlanStatus.Draft);
        return plan;
    }

    public static MonitoringPlan SecondArchiveTestPlan(
        Guid planId,
        Guid installationId,
        ICollection<MonitoringRequirement> requirements)
    {
        var plan = MonitoringPlan.New(
            id: planId,
            installationId: installationId,
            version: "v2",
            notes: "Test monitoring plan v2",
            requirements: requirements
        );

        plan.ChangeStatus(MonitoringPlanStatus.Archived);
        return plan;
    }

    public static MonitoringPlan PlanToCreate(
        Guid installationId,
        ICollection<MonitoringRequirement> requirements)
        => MonitoringPlan.New(
            id: Guid.NewGuid(),
            installationId: installationId,
            version: "create-v1",
            notes: "Created from tests",
            requirements: requirements
        );


    public static MonitoringPlan PlanToUpdate(
        Guid planId,
        Guid installationId,
        ICollection<MonitoringRequirement> requirements)
        => MonitoringPlan.New(
            id: planId,
            installationId: installationId,
            version: "updated-v1",
            notes: "Updated from tests",
            requirements: requirements
        );
}