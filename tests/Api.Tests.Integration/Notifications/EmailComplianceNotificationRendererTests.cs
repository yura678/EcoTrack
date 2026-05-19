using Domain.Entities.Monitoring;
using FluentAssertions;
using Infrastructure.Compliance.Notifications;

namespace Api.Tests.Integration.Notifications;

/// <summary>
/// Pure unit tests for the renderer — no DB, no DI. Validates that per-EventType subject
/// lines mention the right anchor entity (source vs device) and that the body includes the
/// optional ratio/notes only when present, so we don't ship "Ratio: " lines with no value.
/// </summary>
public class EmailComplianceNotificationRendererTests
{
    private readonly EmailComplianceNotificationRenderer _renderer = new();

    private static readonly DateTime WindowStart = new(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 5, 18, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LimitExceedanceSubjectShouldReferenceSource()
    {
        var sourceId = Guid.NewGuid();
        var ev = ComplianceEvent.ForLimitExceedance(
            Guid.NewGuid(), sourceId, measurementId: null,
            limitId: Guid.NewGuid(), ratio: 1.5m,
            WindowStart, WindowEnd, notes: "100 mg/m³ > 50 mg/m³");

        var content = _renderer.Render(ev);

        content.Subject.Should().Contain("Перевищення ліміту");
        content.Subject.Should().Contain(sourceId.ToString());
        content.Body.Should().Contain("LimitExceedance");
        content.Body.Should().Contain(sourceId.ToString());
        content.Body.Should().Contain("Ratio: 1.5");
        content.Body.Should().Contain("100 mg/m³ > 50 mg/m³");
    }

    [Fact]
    public void DeviceOfflineSubjectShouldReferenceDevice()
    {
        var sourceId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), sourceId, deviceId, WindowStart, WindowEnd);

        var content = _renderer.Render(ev);

        content.Subject.Should().Contain("не на зв'язку");
        content.Subject.Should().Contain(deviceId.ToString());
        content.Body.Should().Contain(deviceId.ToString());
    }

    [Fact]
    public void CalibrationFailureSubjectShouldReferenceDevice()
    {
        var deviceId = Guid.NewGuid();
        var ev = ComplianceEvent.ForCalibrationFailure(
            Guid.NewGuid(), Guid.NewGuid(), deviceId, WindowStart, WindowEnd);

        var content = _renderer.Render(ev);

        content.Subject.Should().Contain("Збій калібрування");
        content.Subject.Should().Contain(deviceId.ToString());
    }

    [Fact]
    public void OutOfRangeReadingSubjectShouldReferenceDevice()
    {
        var deviceId = Guid.NewGuid();
        var ev = ComplianceEvent.ForOutOfRangeReading(
            Guid.NewGuid(), Guid.NewGuid(), deviceId, ratio: 0.2m,
            WindowStart, WindowEnd, notes: "12/60 out of range");

        var content = _renderer.Render(ev);

        content.Subject.Should().Contain("Показники поза діапазоном");
        content.Subject.Should().Contain(deviceId.ToString());
        content.Body.Should().Contain("Ratio: 0.2");
        content.Body.Should().Contain("12/60 out of range");
    }

    [Fact]
    public void MissingMeasurementSubjectShouldReferenceSource()
    {
        var sourceId = Guid.NewGuid();
        var ev = ComplianceEvent.ForMissingMeasurement(
            Guid.NewGuid(), sourceId, WindowStart, WindowEnd);

        var content = _renderer.Render(ev);

        content.Subject.Should().Contain("Відсутні вимірювання");
        content.Subject.Should().Contain(sourceId.ToString());
    }

    [Fact]
    public void DataAvailabilityLossSubjectShouldReferenceSource()
    {
        var sourceId = Guid.NewGuid();
        var ev = ComplianceEvent.ForDataAvailabilityLoss(
            Guid.NewGuid(), sourceId, measurementId: null,
            WindowStart, WindowEnd, notes: "30/60 valid (50%)");

        var content = _renderer.Render(ev);

        content.Subject.Should().Contain("Низька доступність");
        content.Subject.Should().Contain(sourceId.ToString());
        content.Body.Should().Contain("30/60 valid (50%)");
    }

    [Fact]
    public void BodyShouldOmitRatioLineWhenRatioMissing()
    {
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WindowStart, WindowEnd);

        var content = _renderer.Render(ev);

        content.Body.Should().NotContain("Ratio:");
    }

    [Fact]
    public void BodyShouldOmitDetailsSectionWhenNotesEmpty()
    {
        var ev = ComplianceEvent.ForDeviceOffline(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WindowStart, WindowEnd, notes: null);

        var content = _renderer.Render(ev);

        content.Body.Should().NotContain("Деталі:");
    }
}
