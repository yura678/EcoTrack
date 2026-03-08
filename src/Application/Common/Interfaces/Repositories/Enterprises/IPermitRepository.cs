using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface IPermitRepository
{
    Task<Option<Permit>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Permit>> GetByNumberAsync(string number, CancellationToken cancellationToken);
    Task<Permit> AddAsync(Permit entity, CancellationToken cancellationToken);
    Permit Update(Permit entity);
    Permit Delete(Permit entity);

    Task<Option<Permit>> GetActiveAsync(
        Guid installationId, PermitType permitType, CancellationToken cancellationToken);

    Task<Option<Permit>> GetActiveByEmissionSourceAsync(
        Guid sourceId, DateTime activeAt, CancellationToken cancellationToken);

    Task<bool> HasDependenciesAsync(Guid permitId, CancellationToken cancellationToken);
}