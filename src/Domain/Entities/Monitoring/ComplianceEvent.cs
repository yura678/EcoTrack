using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;

namespace Domain.Entities.Monitoring;

public class ComplianceEvent : BaseEntity, ITenantOwned
{
    public ComplianceEventType EventType { get; private set; }

    public Guid EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }
    public Guid EnterpriseId { get; private set; }

    public Guid? MeasurementId { get; private set; }
    public Measurement? Measurement { get; private set; }

    public Guid? LimitId { get; private set; }
    public EmissionLimit? Limit { get; private set; }

    public Guid? DeviceId { get; private set; }
    public MonitoringDevice? Device { get; private set; }

    public DateTime WindowStart { get; private set; }
    public DateTime WindowEnd { get; private set; }

    public decimal? Ratio { get; private set; }

    public ComplianceEventStatus Status { get; private set; }
    public DateTime DetectedAt { get; }
    public DateTime? ClosedAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public ResolutionReason? ResolutionReason { get; private set; }
    public string? ResolutionNote { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }

    private ComplianceEvent(Guid id, ComplianceEventType eventType, Guid emissionSourceId,
        Guid? measurementId, Guid? limitId, Guid? deviceId,
        DateTime windowStart, DateTime windowEnd, decimal? ratio,
        ComplianceEventStatus status, DateTime detectedAt, DateTime? closedAt,
        string? notes, DateTime? updatedAt)
    {
        Id = id;
        EventType = eventType;
        EmissionSourceId = emissionSourceId;
        MeasurementId = measurementId;
        LimitId = limitId;
        DeviceId = deviceId;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        Ratio = ratio;
        Status = status;
        DetectedAt = detectedAt;
        ClosedAt = closedAt;
        Notes = notes;
        UpdatedAt = updatedAt;
    }

    public static ComplianceEvent ForLimitExceedance(Guid id, Guid emissionSourceId,
        Guid? measurementId, Guid limitId, decimal ratio,
        DateTime windowStart, DateTime windowEnd, string? notes = null) =>
        new(id, ComplianceEventType.LimitExceedance, emissionSourceId,
            measurementId, limitId, deviceId: null,
            windowStart, windowEnd, ratio,
            ComplianceEventStatus.Open, DateTime.UtcNow, closedAt: null, notes, updatedAt: null);

    public static ComplianceEvent ForDataAvailabilityLoss(Guid id, Guid emissionSourceId,
        Guid? measurementId, DateTime windowStart, DateTime windowEnd, string? notes = null) =>
        new(id, ComplianceEventType.DataAvailabilityLoss, emissionSourceId,
            measurementId, limitId: null, deviceId: null,
            windowStart, windowEnd, ratio: null,
            ComplianceEventStatus.Open, DateTime.UtcNow, closedAt: null, notes, updatedAt: null);

    public static ComplianceEvent ForDeviceOffline(Guid id, Guid emissionSourceId, Guid deviceId,
        DateTime windowStart, DateTime windowEnd, string? notes = null) =>
        new(id, ComplianceEventType.DeviceOffline, emissionSourceId,
            measurementId: null, limitId: null, deviceId,
            windowStart, windowEnd, ratio: null,
            ComplianceEventStatus.Open, DateTime.UtcNow, closedAt: null, notes, updatedAt: null);

    public static ComplianceEvent ForCalibrationFailure(Guid id, Guid emissionSourceId, Guid deviceId,
        DateTime windowStart, DateTime windowEnd, string? notes = null) =>
        new(id, ComplianceEventType.CalibrationFailure, emissionSourceId,
            measurementId: null, limitId: null, deviceId,
            windowStart, windowEnd, ratio: null,
            ComplianceEventStatus.Open, DateTime.UtcNow, closedAt: null, notes, updatedAt: null);

    public static ComplianceEvent ForMissingMeasurement(Guid id, Guid emissionSourceId,
        DateTime windowStart, DateTime windowEnd, string? notes = null) =>
        new(id, ComplianceEventType.MissingMeasurement, emissionSourceId,
            measurementId: null, limitId: null, deviceId: null,
            windowStart, windowEnd, ratio: null,
            ComplianceEventStatus.Open, DateTime.UtcNow, closedAt: null, notes, updatedAt: null);

    public void ChangeStatus(ComplianceEventStatus status)
    {
        if (status == ComplianceEventStatus.Closed)
            throw new InvalidOperationException(
                "Use Close(reason, note, userId) — closing always requires a resolution reason.");

        Status = status;
        if (Status == ComplianceEventStatus.Open)
        {
            // Reopen path: clear resolution metadata so it can't outlive the closure it belonged to.
            ClosedAt = null;
            ResolutionReason = null;
            ResolutionNote = null;
            ResolvedByUserId = null;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Closes the event with a regulator-meaningful classification. ResolutionNote is mandatory
    /// when reason is Other so the audit trail isn't left empty for free-form cases.
    /// </summary>
    public void Close(ResolutionReason reason, string? note, Guid? resolvedByUserId)
    {
        if (reason == Monitoring.ResolutionReason.Other && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException(
                "ResolutionNote is required when ResolutionReason is Other.", nameof(note));

        Status = ComplianceEventStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        ResolutionReason = reason;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ResolvedByUserId = resolvedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignTenant(Guid enterpriseId)
    {
        if (EnterpriseId == Guid.Empty)
        {
            EnterpriseId = enterpriseId;
        }
        else if (EnterpriseId != enterpriseId)
        {
            throw new InvalidOperationException(
                $"EnterpriseId is immutable on ComplianceEvent (current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}
