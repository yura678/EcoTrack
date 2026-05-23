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
/// sees ComplianceEvent open/close transitions without polling.
/// </summary>
public class BroadcastComplianceEventHandler(
    IComplianceEventQueries queries,
    IHubContext<ComplianceEventsHub> hub,
    ILogger<BroadcastComplianceEventHandler> logger)
    : INotificationHandler<ComplianceEventOpenedNotification>,
      INotificationHandler<ComplianceEventClosedNotification>
{
    public Task Handle(ComplianceEventOpenedNotification notification, CancellationToken cancellationToken) =>
        BroadcastByIdAsync(notification.ComplianceEventId, ComplianceEventsHub.EventOpenedMethod, cancellationToken);

    public Task Handle(ComplianceEventClosedNotification notification, CancellationToken cancellationToken) =>
        BroadcastByIdAsync(notification.ComplianceEventId, ComplianceEventsHub.EventClosedMethod, cancellationToken);

    private async Task BroadcastByIdAsync(Guid eventId, string method, CancellationToken cancellationToken)
    {
        var option = await queries.GetByIdAsync(eventId, cancellationToken);
        await option.Match(
            Some: ev => BroadcastAsync(ev, method, cancellationToken),
            None: () =>
            {
                logger.LogWarning(
                    "ComplianceEvent {EventId} disappeared before SignalR {Method} broadcast",
                    eventId, method);
                return Task.CompletedTask;
            });
    }

    private async Task BroadcastAsync(
        Domain.Entities.Monitoring.ComplianceEvent ev, string method, CancellationToken cancellationToken)
    {
        try
        {
            var dto = ComplianceEventDto.FromDomainModel(ev);
            await hub.Clients
                .Group(ComplianceEventsHub.GroupName(ev.EnterpriseId))
                .SendAsync(method, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            // Broadcast failures are fire-and-forget — losing the in-app push doesn't change
            // compliance correctness (REST + email/webhook still cover the persistent flow).
            logger.LogError(ex, "Failed to broadcast event {EventId} {Method} to SignalR group",
                ev.Id, method);
        }
    }
}
