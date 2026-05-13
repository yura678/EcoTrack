using Domain.Common;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Domain.Entities.EmissionSources;

public class Pollutant : BaseEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? CasNumber { get; private set; }
    public PollutantCategory Category { get; private set; }
    public PollutantMedia Media { get; private set; }
    public MeasureUnitDimension DefaultDimension { get; private set; }
    public decimal? DefaultO2Reference { get; private set; }
    public decimal? EprtrThresholdKgYear { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<EmissionLimit>? EmissionLimits { get; private set; } = [];
    public ICollection<Measurement>? Measurements { get; private set; } = [];
    public ICollection<MonitoringRequirement>? MonitoringRequirements { get; private set; } = [];

    private Pollutant(Guid id, string code, string name, string? casNumber,
        PollutantCategory category, PollutantMedia media,
        MeasureUnitDimension defaultDimension, decimal? defaultO2Reference,
        decimal? eprtrThresholdKgYear,
        DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        Code = code;
        Name = name;
        CasNumber = casNumber;
        Category = category;
        Media = media;
        DefaultDimension = defaultDimension;
        DefaultO2Reference = defaultO2Reference;
        EprtrThresholdKgYear = eprtrThresholdKgYear;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Pollutant New(Guid id, string code, string name,
        PollutantCategory category, PollutantMedia media, MeasureUnitDimension defaultDimension,
        string? casNumber = null, decimal? defaultO2Reference = null,
        decimal? eprtrThresholdKgYear = null) =>
        new(id, code, name, casNumber, category, media, defaultDimension,
            defaultO2Reference, eprtrThresholdKgYear, DateTime.UtcNow, null);

    public void UpdateDetails(string code, string name, string? casNumber,
        PollutantCategory category, PollutantMedia media,
        MeasureUnitDimension defaultDimension, decimal? defaultO2Reference,
        decimal? eprtrThresholdKgYear)
    {
        Code = code;
        Name = name;
        CasNumber = casNumber;
        Category = category;
        Media = media;
        DefaultDimension = defaultDimension;
        DefaultO2Reference = defaultO2Reference;
        EprtrThresholdKgYear = eprtrThresholdKgYear;

        UpdatedAt = DateTime.UtcNow;
    }
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
