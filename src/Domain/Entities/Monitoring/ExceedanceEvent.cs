using Domain.Common;
using Domain.Entities.Enterprises;

namespace Domain.Entities.Monitoring;

public class ExceedanceEvent : BaseEntity
{
    public Guid MeasurementId { get; }
    public Measurement? Measurement { get; private set; }
    public Guid LimitId { get; }
    public EmissionLimit? Limit { get; private set; }
    public decimal Magnitude { get; private set; }
    public ExceedanceEventStatus Status { get; private set; }
    public DateTime DetectedAt { get; }
    public DateTime? UpdatedAt { get; private set; }
    public string? Notes { get; private set; }

    private ExceedanceEvent(Guid id, Guid measurementId,
        Guid limitId, decimal magnitude, ExceedanceEventStatus status, DateTime detectedAt,
        DateTime? updatedAt, string? notes)
    {
        Id = id;
        MeasurementId = measurementId;
        LimitId = limitId;
        Magnitude = magnitude;
        Status = status;
        DetectedAt = detectedAt;
        UpdatedAt = updatedAt;
        Notes = notes;
    }

    public static ExceedanceEvent New(Guid id, Guid measurementId,
        Guid limitId, decimal magnitude, ExceedanceEventStatus status,
        string? notes) => new(id, measurementId, limitId, magnitude, status, DateTime.UtcNow, null, notes);

    public void ChangeStatus(ExceedanceEventStatus status)
    {
        Status = status;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum ExceedanceEventStatus
{
    Open = 0,
    Investigating = 1,
    Closed = 2
}