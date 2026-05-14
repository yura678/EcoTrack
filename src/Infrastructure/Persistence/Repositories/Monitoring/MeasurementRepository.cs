using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MeasurementRepository(ApplicationDbContext context)
    : BaseAsyncRepository<Measurement>(context), IMeasurementRepository, IMeasurementQueries
{
    public async Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? Option<Measurement>.None;
    }

    public async Task<Option<Measurement>> GetByTimeStamp(DateTime timestamp, Guid pollutantId,
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.WindowEnd == timestamp
                                      && x.PollutantId == pollutantId
                                      && x.EmissionSourceId == emissionSourceId,
                cancellationToken);

        return entity ?? Option<Measurement>.None;
    }

    public async Task<PageResult<Measurement>> GetPagedAsync(
        Guid installationId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = base.TableNoTracking
            .Where(x => x.EmissionSource!.InstallationId == installationId);

        if (from.HasValue)
        {
            query = query.Where(x => x.WindowEnd >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.WindowEnd <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.WindowEnd)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Measurement>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
