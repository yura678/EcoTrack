using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IMeasureUnitQueries
{
    Task<IReadOnlyList<MeasureUnit>> GetAllAsync(CancellationToken cancellationToken);
    Task<Option<MeasureUnit>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}