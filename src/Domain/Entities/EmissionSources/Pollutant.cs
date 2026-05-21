using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Domain.Entities.EmissionSources;

public class Pollutant : BaseEntity, ISoftDeletable
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? CasNumber { get; private set; }
    public PollutantCategory Category { get; private set; }
    public PollutantMedia Media { get; private set; }
    public MeasureUnitDimension DefaultDimension { get; private set; }

    // The single unit every materialized Measurement and CA bucket for this pollutant is stored
    // in. Picked once at seed time per pollutant (mg/m³ for gases, µg/m³ for trace metals, etc).
    // Cross-device temporal queries are correct because every historical row converges here.
    public Guid CanonicalUnitId { get; private set; }
    public MeasureUnit? CanonicalUnit { get; private set; }

    // Required to convert ppm (volume ratio) into mass concentration via the ideal-gas relation
    // mg/m³ = ppm × M / 22.414 at EU STP (273.15 K, 1.013 bar, dry). Null is acceptable for
    // pollutants where ppm-style ingest is not expected (PM, condensed phases).
    public decimal? MolarMass { get; private set; }

    public decimal? DefaultO2Reference { get; private set; }
    public decimal? EprtrThresholdKgYear { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public ICollection<EmissionLimit>? EmissionLimits { get; private set; } = [];
    public ICollection<Measurement>? Measurements { get; private set; } = [];

    private Pollutant(Guid id, string code, string name, string? casNumber,
        PollutantCategory category, PollutantMedia media,
        MeasureUnitDimension defaultDimension, Guid canonicalUnitId, decimal? molarMass,
        decimal? defaultO2Reference, decimal? eprtrThresholdKgYear,
        DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        Code = code;
        Name = name;
        CasNumber = casNumber;
        Category = category;
        Media = media;
        DefaultDimension = defaultDimension;
        CanonicalUnitId = canonicalUnitId;
        MolarMass = molarMass;
        DefaultO2Reference = defaultO2Reference;
        EprtrThresholdKgYear = eprtrThresholdKgYear;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Pollutant New(Guid id, string code, string name,
        PollutantCategory category, PollutantMedia media, MeasureUnitDimension defaultDimension,
        Guid canonicalUnitId,
        string? casNumber = null, decimal? molarMass = null,
        decimal? defaultO2Reference = null, decimal? eprtrThresholdKgYear = null) =>
        new(id, code, name, casNumber, category, media, defaultDimension,
            canonicalUnitId, molarMass, defaultO2Reference, eprtrThresholdKgYear,
            DateTime.UtcNow, null);

    public void UpdateDetails(string code, string name, string? casNumber,
        PollutantCategory category, PollutantMedia media,
        MeasureUnitDimension defaultDimension, Guid canonicalUnitId, decimal? molarMass,
        decimal? defaultO2Reference, decimal? eprtrThresholdKgYear)
    {
        Code = code;
        Name = name;
        CasNumber = casNumber;
        Category = category;
        Media = media;
        DefaultDimension = defaultDimension;
        CanonicalUnitId = canonicalUnitId;
        MolarMass = molarMass;
        DefaultO2Reference = defaultO2Reference;
        EprtrThresholdKgYear = eprtrThresholdKgYear;

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted() => DeletedAt = DateTime.UtcNow;
    public void Restore() => DeletedAt = null;
}

public enum PollutantCategory
{
    Gas = 0,
    ParticulateMatter = 1,
    HeavyMetal = 2,
    Voc = 3,
    Acid = 4,
    Inorganic = 5,
    Organic = 6,
    Other = 99
}

public enum PollutantMedia
{
    Air = 0,
    Water = 1,
    Both = 2
}
