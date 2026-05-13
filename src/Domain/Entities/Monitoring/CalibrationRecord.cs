using Domain.Common;

namespace Domain.Entities.Monitoring;

public class CalibrationRecord : BaseEntity
{
    public Guid DeviceId { get; private set; }
    public MonitoringDevice? Device { get; private set; }
    public CalibrationType Type { get; private set; }
    public DateTime PerformedAt { get; private set; }
    public DateTime NextDueAt { get; private set; }
    public CalibrationResult Result { get; private set; }
    public string PerformedBy { get; private set; }
    public string? CertificateNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    private CalibrationRecord(Guid id, Guid deviceId, CalibrationType type,
        DateTime performedAt, DateTime nextDueAt, CalibrationResult result,
        string performedBy, string? certificateNumber, string? notes,
        DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        DeviceId = deviceId;
        Type = type;
        PerformedAt = performedAt;
        NextDueAt = nextDueAt;
        Result = result;
        PerformedBy = performedBy;
        CertificateNumber = certificateNumber;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static CalibrationRecord New(Guid id, Guid deviceId, CalibrationType type,
        DateTime performedAt, DateTime nextDueAt, CalibrationResult result,
        string performedBy, string? certificateNumber, string? notes) =>
        new(id, deviceId, type, performedAt, nextDueAt, result, performedBy,
            certificateNumber, notes, DateTime.UtcNow, null);

    public void UpdateDetails(CalibrationType type, DateTime performedAt, DateTime nextDueAt,
        CalibrationResult result, string performedBy, string? certificateNumber, string? notes)
    {
        Type = type;
        PerformedAt = performedAt;
        NextDueAt = nextDueAt;
        Result = result;
        PerformedBy = performedBy;
        CertificateNumber = certificateNumber;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum CalibrationType
{
    Qal2 = 0,    // QAL2 — ринкова перевірка (EN 14181)
    Ast = 1,     // Annual Surveillance Test
    Span = 2,
    Zero = 3,
    Internal = 4
}

public enum CalibrationResult
{
    Pass = 0,
    Fail = 1
}
