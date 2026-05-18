using Application.Common.Interfaces.Queries.Notifications;
using Application.Common.Interfaces.Repositories.Notifications;
using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Notifications;

internal class NotificationSubscriptionRepository(ApplicationDbContext context)
    : BaseAsyncRepository<NotificationSubscription>(context),
        INotificationSubscriptionRepository,
        INotificationSubscriptionQueries
{
    public async Task<Option<NotificationSubscription>> GetByIdAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var entity = await Table.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity ?? Option<NotificationSubscription>.None;
    }

    public async Task<IReadOnlyList<NotificationSubscription>> GetByUserAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        return await TableNoTracking
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationSubscription>> GetMatchingForEventAsync(
        Guid enterpriseId,
        ComplianceEventType eventType,
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        // Filter array semantics: null means "any". PG translates `x ?| array[...]` for the
        // any-match cases. We pull only enabled rows that pass the array contains check —
        // remaining filter logic stays in C# because the array intersection per row is small
        // and EF Core's PG provider has uneven support for parameterised array contains across
        // nullable arrays.
        var rows = await TableNoTracking
            .Where(x => x.EnterpriseId == enterpriseId && x.IsEnabled)
            .ToListAsync(cancellationToken);

        return rows
            .Where(s => (s.EventTypes == null || s.EventTypes.Contains(eventType))
                        && (s.EmissionSourceIds == null || s.EmissionSourceIds.Contains(emissionSourceId)))
            .ToList();
    }
}
