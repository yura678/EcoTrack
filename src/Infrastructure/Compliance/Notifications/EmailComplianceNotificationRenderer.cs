using System.Globalization;
using System.Text;
using Application.Common.Interfaces.Notifications;
using Domain.Entities.Monitoring;

namespace Infrastructure.Compliance.Notifications;

/// <summary>
/// Builds plain-text email content for compliance events. Per-type subject lines keep the
/// inbox triageable; the body always includes the window, ratio (when applicable), and the
/// detector's free-text Notes so the recipient can act without opening the UI.
/// </summary>
public class EmailComplianceNotificationRenderer : IEmailComplianceNotificationRenderer
{
    public EmailComplianceNotificationContent Render(ComplianceEvent ev)
    {
        var subject = ev.EventType switch
        {
            ComplianceEventType.LimitExceedance =>
                $"[EcoTrack] Перевищення ліміту на джерелі {ev.EmissionSourceId}",
            ComplianceEventType.DataAvailabilityLoss =>
                $"[EcoTrack] Низька доступність даних на джерелі {ev.EmissionSourceId}",
            ComplianceEventType.DeviceOffline =>
                $"[EcoTrack] Прилад {ev.DeviceId} не на зв'язку",
            ComplianceEventType.CalibrationFailure =>
                $"[EcoTrack] Збій калібрування приладу {ev.DeviceId}",
            ComplianceEventType.MissingMeasurement =>
                $"[EcoTrack] Відсутні вимірювання на джерелі {ev.EmissionSourceId}",
            ComplianceEventType.OutOfRangeReading =>
                $"[EcoTrack] Показники поза діапазоном на приладі {ev.DeviceId}",
            _ => $"[EcoTrack] Compliance event {ev.Id}"
        };

        var body = new StringBuilder();
        body.AppendLine($"Тип події: {ev.EventType}");
        body.AppendLine($"Виявлено: {ev.DetectedAt:O}");
        body.AppendLine($"Вікно: {ev.WindowStart:O} — {ev.WindowEnd:O}");
        body.AppendLine($"Джерело: {ev.EmissionSourceId}");
        if (ev.DeviceId.HasValue) body.AppendLine($"Прилад: {ev.DeviceId.Value}");
        if (ev.LimitId.HasValue) body.AppendLine($"Ліміт: {ev.LimitId.Value}");
        if (ev.Ratio.HasValue)
        {
            body.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"Ratio: {ev.Ratio.Value:0.##}"));
        }
        if (!string.IsNullOrWhiteSpace(ev.Notes))
        {
            body.AppendLine();
            body.AppendLine("Деталі:");
            body.AppendLine(ev.Notes);
        }

        return new EmailComplianceNotificationContent(subject, body.ToString());
    }
}
