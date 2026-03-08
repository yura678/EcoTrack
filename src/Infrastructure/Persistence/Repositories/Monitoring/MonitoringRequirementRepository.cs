using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MonitoringRequirementRepository(ApplicationDbContext context)
    : BaseAsyncRepository<MonitoringRequirement>(context), IMonitoringRequirementRepository
{
    public IReadOnlyList<MonitoringRequirement> UpdateRange(
        IReadOnlyList<MonitoringRequirement> entities)
    {
        Entities.UpdateRange(entities);

        return entities;
    }

    public async Task<IReadOnlyList<MonitoringRequirement>> AddRangeAsync(
        IReadOnlyList<MonitoringRequirement> entities, CancellationToken cancellationToken)
    {
        await Entities.AddRangeAsync(entities, cancellationToken);
        return entities;
    }

    public IReadOnlyList<MonitoringRequirement> RemoveRange(
        IReadOnlyList<MonitoringRequirement> entities)
    {
        Entities.RemoveRange(entities);
        return entities;
    }

    public async Task<Option<MonitoringRequirement>> FindRequirementAsync(
        Guid sourceId,
        Guid pollutantId,
        CancellationToken cancellationToken)
    {
        return await base.Table
            .Include(x => x.MonitoringPlan)
            .Where(x =>
                x.EmissionSourceId.Equals(sourceId) &&
                x.PollutantId.Equals(pollutantId) &&
                x.MonitoringPlan.Status == MonitoringPlanStatus.Active
            )
            .FirstOrDefaultAsync(cancellationToken);
    }
}