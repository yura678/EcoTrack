using Application.Common.Interfaces.Queries.Emissions;
using Application.Common.Interfaces.Repositories.Emissions;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.EmissionSources;

internal class PollutantRepository(ApplicationDbContext context)
    : BaseAsyncRepository<Pollutant>(context), IPollutantQueries, IPollutantRepository
{
    public async Task<IReadOnlyList<Pollutant>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        var hasDependencies = await DbContext.Set<Measurement>().AnyAsync(x => x.PollutantId.Equals(id),
                                  cancellationToken)
                              || await DbContext.Set<EmissionLimit>().AnyAsync(x => x.PollutantId.Equals(id),
                                  cancellationToken)
                              || await DbContext.Set<MonitoringRequirement>().AnyAsync(x => x.PollutantId.Equals(id),
                                  cancellationToken);

        return hasDependencies;
    }

    public async Task<Option<Pollutant>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);
    }

    public async Task<IReadOnlyList<Pollutant>> GetByIdsAsync(IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<Pollutant>> GetByCodeAsync(string code,
        CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Code.Equals(code), cancellationToken);
    }

    public async Task<Option<Pollutant>> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Name.Equals(name), cancellationToken);
    }
}