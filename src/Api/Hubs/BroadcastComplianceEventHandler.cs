using Api.Dtos;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Features.ComplianceEvents.Notifications;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Api.Hubs;

/// <summary>
/// Sibling notification handler to <see cref="Infrastructure.Compliance.Notifications.EnqueueComplianceNotificationsHandler"/>:
/// where that one enqueues async email/webhook delivery via Hangfire, this one fires a
/// fire-and-forget SignalR broadcast so any browser tab logged into the event's enterprise
/// sees the new ComplianceEvent without polling.
/// </summary>
public class BroadcastComplianceEventHandler(
    IComplianceEventQueries queries,
    IHubContext<ComplianceEventsHub> hub,
    ILogger<BroadcastComplianceEventHandler> logger)
    : INotificationHandler<ComplianceEventOpenedNotification>
{
    public async Task Handle(ComplianceEventOpenedNotification notification, CancellationToken cancellationToken)
    {
        var option = await queries.GetByIdAsync(notification.ComplianceEventId, cancellationToken);
        var ev = option.Match(e => e, () => null!);
        if (ev is null)
        {
            logger.LogWarning(
                "ComplianceEvent {EventId} disappeared before SignalR broadcast",
                notification.ComplianceEventId);
            return;
        }

        try
        {
            var dto = ComplianceEventDto.FromDomainModel(ev);
            await hub.Clients
                .Group(ComplianceEventsHub.GroupName(ev.EnterpriseId))
                .SendAsync(ComplianceEventsHub.EventOpenedMethod, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            // Broadcast failures are fire-and-forget — losing the in-app push doesn't change
            // compliance correctness (REST + email/webhook still cover the persistent flow).
            logger.LogError(ex, "Failed to broadcast event {EventId} to SignalR group", ev.Id);
        }
    }
}
