using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Notifications;

public record EmailComplianceNotificationContent(string Subject, string Body);

public interface IEmailComplianceNotificationRenderer
{
    /// <summary>
    /// Builds the (subject, body) pair for a notification email about a single ComplianceEvent.
    /// The implementation owns the per-EventType wording. Body is plain text for now; can
    /// switch to HTML later without changing this contract.
    /// </summary>
    EmailComplianceNotificationContent Render(ComplianceEvent complianceEvent);
}
