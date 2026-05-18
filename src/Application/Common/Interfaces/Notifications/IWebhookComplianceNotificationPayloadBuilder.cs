using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Notifications;

public interface IWebhookComplianceNotificationPayloadBuilder
{
    /// <summary>
    /// Serializes the compliance event to the wire JSON contract sent to webhook subscribers.
    /// </summary>
    string Build(ComplianceEvent complianceEvent);
}
