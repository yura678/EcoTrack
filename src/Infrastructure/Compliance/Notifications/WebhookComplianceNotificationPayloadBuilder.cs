using System.Text.Json;
using Application.Common.Interfaces.Notifications;
using Domain.Entities.Monitoring;

namespace Infrastructure.Compliance.Notifications;

public class WebhookComplianceNotificationPayloadBuilder : IWebhookComplianceNotificationPayloadBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string Build(ComplianceEvent ev)
    {
        var payload = new
        {
            id = ev.Id,
            eventType = ev.EventType.ToString(),
            status = ev.Status.ToString(),
            enterpriseId = ev.EnterpriseId,
            emissionSourceId = ev.EmissionSourceId,
            deviceId = ev.DeviceId,
            limitId = ev.LimitId,
            measurementId = ev.MeasurementId,
            windowStart = ev.WindowStart,
            windowEnd = ev.WindowEnd,
            detectedAt = ev.DetectedAt,
            ratio = ev.Ratio,
            notes = ev.Notes
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
