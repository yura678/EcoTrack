using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IMonitoringRequirementRepository
{
    IReadOnlyList<MonitoringRequirement> RemoveRange(
        IReadOnlyList<MonitoringRequirement> entities);

    IReadOnlyList<MonitoringRequirement> UpdateRange(
        IReadOnlyList<MonitoringRequirement> entities);

    Task<IReadOnlyList<MonitoringRequirement>> AddRangeAsync(
        IReadOnlyList<MonitoringRequirement> entities, CancellationToken cancellationToken);


    Task<Option<MonitoringRequirement>> FindRequirementAsync(Guid emissionSourceId,
        Guid pollutantId, CancellationToken cancellationToken);
}