using Domain.Entities.Enterprises;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface IEnterpriseRepository
{
    Task<Option<Enterprise>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Option<Enterprise>> GetByEdrpouAsync(string edrpou, CancellationToken cancellationToken);
    Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken);

    Task<Enterprise> AddAsync(Enterprise entity, CancellationToken cancellationToken);
    Enterprise Update(Enterprise entity);
    Enterprise Delete(Enterprise entity);
}