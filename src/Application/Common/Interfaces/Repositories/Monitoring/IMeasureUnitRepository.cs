using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface IMeasureUnitRepository
{
    Task<Option<MeasureUnit>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeasureUnit>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}