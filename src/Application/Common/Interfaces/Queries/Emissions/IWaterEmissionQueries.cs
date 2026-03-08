using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Emissions;

public interface IWaterEmissionQueries
{
    Task<IReadOnlyList<WaterEmissionSource>> GetByInstallationIdAsync(Guid installationId,
        CancellationToken cancellationToken);

    Task<Option<WaterEmissionSource>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}