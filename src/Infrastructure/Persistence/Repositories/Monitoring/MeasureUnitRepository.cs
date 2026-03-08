using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MeasureUnitRepository(ApplicationDbContext context)
    : BaseAsyncRepository<MeasureUnit>(context), IMeasureUnitQueries, IMeasureUnitRepository
{
    public async Task<IReadOnlyList<MeasureUnit>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<MeasureUnit>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

        return entity ?? Option<MeasureUnit>.None;
    }

    public async Task<IReadOnlyList<MeasureUnit>> GetByIdsAsync(IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }
}