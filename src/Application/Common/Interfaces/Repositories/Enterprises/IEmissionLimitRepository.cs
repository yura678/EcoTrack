using Domain.Entities.Enterprises;

namespace Application.Common.Interfaces.Repositories.Enterprises;

public interface IEmissionLimitRepository
{
    IReadOnlyList<EmissionLimit> RemoveRange(
        IReadOnlyList<EmissionLimit> entities);
    
    IReadOnlyList<EmissionLimit> UpdateRange(
        IReadOnlyList<EmissionLimit> entities);
    
    Task<IReadOnlyList<EmissionLimit>> AddRangeAsync(
        IReadOnlyList<EmissionLimit> entities,
        CancellationToken cancellationToken);
}