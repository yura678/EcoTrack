using Application.Common.Interfaces.Repositories.Enterprises;
using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Repositories.Common;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class EmissionLimitRepository(ApplicationDbContext context)
    : BaseAsyncRepository<EmissionLimit>(context), IEmissionLimitRepository
{
    public IReadOnlyList<EmissionLimit> UpdateRange(
        IReadOnlyList<EmissionLimit> entities)
    {
        Entities.UpdateRange(entities);
        return entities;
    }

    public async Task<IReadOnlyList<EmissionLimit>> AddRangeAsync(
        IReadOnlyList<EmissionLimit> entities,
        CancellationToken cancellationToken)
    {
        await Entities.AddRangeAsync(entities, cancellationToken);
        return entities;
    }


    public IReadOnlyList<EmissionLimit> RemoveRange(
        IReadOnlyList<EmissionLimit> entities)
    {
        Entities.RemoveRange(entities);
        return entities;
    }
}