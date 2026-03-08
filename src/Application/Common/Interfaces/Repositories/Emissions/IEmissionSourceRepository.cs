using Domain.Entities.EmissionSources;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Emissions;

public interface IEmissionSourceRepository
{
    Task<EmissionSource> AddAsync(EmissionSource entity, CancellationToken cancellationToken);
    EmissionSource Update(EmissionSource entity);
    EmissionSource Delete(EmissionSource entity);
    Task<Option<EmissionSource>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmissionSource>> GetByEnterpriseAndIdsAsync(
        Guid enterpriseId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EmissionSource>> GetByInstallationAndIdsAsync(
        Guid installationId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EmissionSource>> GetByInstallationIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<EmissionSource>> GetByCodeAsync(Guid id, string code,
        CancellationToken cancellationToken);

    Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken);
}