using System.Text.Json;
using Domain.Entities.Monitoring;
using FluentAssertions;
using Infrastructure.Compliance.Notifications;

namespace Api.Tests.Integration.Notifications;

/// <summary>
/// JSON-contract tests. The webhook payload is a public surface for subscribers, so the
/// field names + value shapes are pinned here; any accidental rename or shape change in the
/// builder breaks these tests loudly instead of silently breaking subscribers in prod.
/// </summary>
public class WebhookComplianceNotificationPayloadBuilderTests
{
    private readonly WebhookComplianceNotificationPayloadBuilder _builder = new();

    [Fact]
    public void PayloadShouldUseCamelCaseKeys()
    {
        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.2m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        var json = _builder.Build(ev);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("id").GetGuid().Should().Be(ev.Id);
        root.GetProperty("eventType").GetString().Should().Be("OutOfRangeReading");
        root.GetProperty("status").GetString().Should().Be("Open");
        root.GetProperty("emissionSourceId").GetGuid().Should().Be(ev.EmissionSourceId);
        root.GetProperty("windowStart").GetDateTime().Should().Be(ev.WindowStart);
        root.GetProperty("windowEnd").GetDateTime().Should().Be(ev.WindowEnd);
    }

    [Fact]
    public void PayloadShouldDropNullOptionalFields()
    {
        // DeviceOffline → no LimitId, no MeasurementId, no Ratio, no Notes
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: null);

        var json = _builder.Build(ev);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("limitId", out _).Should().BeFalse();
        root.TryGetProperty("measurementId", out _).Should().BeFalse();
        root.TryGetProperty("ratio", out _).Should().BeFalse();
        root.TryGetProperty("notes", out _).Should().BeFalse();
        // DeviceId IS present for DeviceOffline.
        root.GetProperty("deviceId").GetGuid().Should().Be(ev.DeviceId!.Value);
    }

    [Fact]
    public void PayloadShouldIncludeRatioAndNotesWhenSet()
    {
        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), Guid.NewGuid(), measurementId: null,
            limitId: Guid.NewGuid(), ratio: 1.5m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            notes: "100 mg/m³ > 50 mg/m³");

        var json = _builder.Build(ev);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("ratio").GetDecimal().Should().Be(1.5m);
        root.GetProperty("notes").GetString().Should().Be("100 mg/m³ > 50 mg/m³");
        root.GetProperty("limitId").GetGuid().Should().Be(ev.LimitId!.Value);
    }

    [Fact]
    public void PayloadShouldSerializeEnumsAsStrings()
    {
        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1.5m,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        var json = _builder.Build(ev);

        json.Should().Contain("\"eventType\":\"LimitExceedance\"");
        json.Should().Contain("\"status\":\"Open\"");
        // Make sure they're NOT numeric.
        json.Should().NotContain("\"eventType\":0");
    }
}
