using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;

namespace Domain.Entities.Enterprises;

public class EmissionLimit : BaseEntity
{
    public decimal Value { get; private set; }

    public AveragingWindow Period { get; private set; }
    public Guid UnitId { get; private set; }
    public MeasureUnit? Unit { get; private set; }

    public Guid PermitId { get; private set; }
    public Permit? Permit { get; private set; }

    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }

    public Guid EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }

    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    private EmissionLimit(Guid id, decimal value, AveragingWindow period,
        Guid permitId, Guid unitId, Guid pollutantId,
        Guid emissionSourceId, DateTime? validFrom, DateTime? validTo)
    {
        Id = id;
        Value = value;
        Period = period;
        UnitId = unitId;
        PermitId = permitId;
        PollutantId = pollutantId;
        EmissionSourceId = emissionSourceId;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public static EmissionLimit New(Guid id, decimal value, AveragingWindow period,
        Guid permitId, Guid unitId, Guid pollutantId,
        Guid emissionSourceId, DateTime? validFrom, DateTime? validTo) =>
        new(id, value, period, permitId, unitId, pollutantId, emissionSourceId, validFrom, validTo);


    public void UpdateDetails(decimal value, AveragingWindow period, Guid unitId,
        Guid pollutantId, Guid emissionSourceId, DateTime? validFrom, DateTime? validTo)
    {
        Value = value;
        Period = period;
        UnitId = unitId;
        PollutantId = pollutantId;
        EmissionSourceId = emissionSourceId;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }
}