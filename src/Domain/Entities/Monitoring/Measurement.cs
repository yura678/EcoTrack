using Domain.Common;
using Domain.Entities.EmissionSources;

namespace Domain.Entities.Monitoring;

public class Measurement : BaseEntity, ITenantOwned
{
    public DateTime WindowStart { get; private set; }
    public DateTime WindowEnd { get; private set; }
    public AveragingWindow Window { get; private set; }
    public Aggregation Aggregation { get; private set; }

    public Guid EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }
    public Guid EnterpriseId { get; private set; }

    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }

    public Guid DeviceId { get; private set; }
    public MonitoringDevice? Device { get; private set; }

    public Guid UnitId { get; private set; }
    public MeasureUnit? Unit { get; private set; }

    public decimal Value { get; private set; }
    public decimal? NormalizedValue { get; private set; }
    public decimal? Uncertainty { get; private set; }

    public int ValidPointsCount { get; private set; }
    public int ExpectedPointsCount { get; private set; }

    // DataAvailability = ValidPointsCount / ExpectedPointsCount (0..1)
    public decimal DataAvailability =>
        ExpectedPointsCount == 0 ? 0m : Math.Round((decimal)ValidPointsCount / ExpectedPointsCount, 4);

    // IsRepresentative per IED: DataAvailability >= 0.75
    public bool IsRepresentative => DataAvailability >= 0.75m;

    public Quality Quality { get; private set; }
    public string? QualityNote { get; private set; }

    public SubstitutionSource? SubstitutedBy { get; private set; }
    public string? SubstitutionReason { get; private set; }
    public DateTime? SubstitutedAt { get; private set; }

    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    private Measurement(Guid id, DateTime windowStart, DateTime windowEnd,
        AveragingWindow window, Aggregation aggregation,
        Guid emissionSourceId, Guid pollutantId, Guid deviceId, Guid unitId,
        decimal value, decimal? normalizedValue, decimal? uncertainty,
        int validPointsCount, int expectedPointsCount,
        Quality quality, string? qualityNote, DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        Window = window;
        Aggregation = aggregation;
        EmissionSourceId = emissionSourceId;
        PollutantId = pollutantId;
        DeviceId = deviceId;
        UnitId = unitId;
        Value = value;
        NormalizedValue = normalizedValue;
        Uncertainty = uncertainty;
        ValidPointsCount = validPointsCount;
        ExpectedPointsCount = expectedPointsCount;
        Quality = quality;
        QualityNote = qualityNote;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Measurement New(Guid id, DateTime windowStart, DateTime windowEnd,
        AveragingWindow window, Aggregation aggregation,
        Guid emissionSourceId, Guid pollutantId, Guid deviceId, Guid unitId,
        decimal value, int validPointsCount, int expectedPointsCount,
        decimal? normalizedValue = null, decimal? uncertainty = null,
        string? qualityNote = null) =>
        new(id, windowStart, windowEnd, window, aggregation,
            emissionSourceId, pollutantId, deviceId, unitId,
            value, normalizedValue, uncertainty,
            validPointsCount, expectedPointsCount,
            Quality.Valid, qualityNote, DateTime.UtcNow, null);

    /// <summary>
    /// Re-applies a freshly computed aggregate over the same window, used when late-arriving
    /// raw_measurement data lands in an already-materialized bucket. Resets quality to Valid
    /// and clears any prior substitution metadata so the caller can re-decide based on the new
    /// availability.
    /// </summary>
    public void RecomputeAggregate(
        decimal value,
        decimal? normalizedValue,
        int validPointsCount,
        int expectedPointsCount,
        Guid unitId)
    {
        Value = value;
        NormalizedValue = normalizedValue;
        ValidPointsCount = validPointsCount;
        ExpectedPointsCount = expectedPointsCount;
        UnitId = unitId;
        Quality = Quality.Valid;
        QualityNote = null;
        SubstitutedBy = null;
        SubstitutionReason = null;
        SubstitutedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkQuality(Quality quality, string? note)
    {
        Quality = quality;
        QualityNote = note;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSubstituted(SubstitutionSource by, string reason)
    {
        Quality = Quality.Substituted;
        SubstitutedBy = by;
        SubstitutionReason = reason;
        SubstitutedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// IED Annex V Part 4 substitution: replaces the measured Value with a conservative
    /// substitute (typically MAX of recent valid windows × 1.05). Normalisation is dropped
    /// because the substitute is already a worst-case proxy.
    /// </summary>
    public void MarkSubstituted(SubstitutionSource by, string reason, decimal substituteValue)
    {
        Value = substituteValue;
        NormalizedValue = null;
        Quality = Quality.Substituted;
        SubstitutedBy = by;
        SubstitutionReason = reason;
        SubstitutedAt = DateTime.UtcNow;
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
                $"EnterpriseId is immutable on Measurement (current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}
