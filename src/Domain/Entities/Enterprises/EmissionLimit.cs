using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;

namespace Domain.Entities.Enterprises;

public class EmissionLimit : BaseEntity, ITenantOwned
{
    public decimal Value { get; private set; }
    public LimitType LimitType { get; private set; }
    public AveragingWindow Period { get; private set; }
    public Guid EnterpriseId { get; private set; }

    public Guid UnitId { get; private set; }
    public MeasureUnit? Unit { get; private set; }

    public Guid PermitId { get; private set; }
    public Permit? Permit { get; private set; }

    public Guid PollutantId { get; private set; }
    public Pollutant? Pollutant { get; private set; }

    public Guid? EmissionSourceId { get; private set; }
    public EmissionSource? EmissionSource { get; private set; }

    public Guid? InstallationId { get; private set; }
    public Installation? Installation { get; private set; }

    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    private EmissionLimit(Guid id, decimal value, LimitType limitType, AveragingWindow period,
        Guid permitId, Guid unitId, Guid pollutantId,
        Guid? emissionSourceId, Guid? installationId,
        DateTime validFrom, DateTime? validTo)
    {
        if ((emissionSourceId is null) == (installationId is null))
        {
            throw new InvalidOperationException(
                "EmissionLimit must target exactly one of EmissionSource or Installation.");
        }

        Id = id;
        Value = value;
        LimitType = limitType;
        Period = period;
        UnitId = unitId;
        PermitId = permitId;
        PollutantId = pollutantId;
        EmissionSourceId = emissionSourceId;
        InstallationId = installationId;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public static EmissionLimit New(Guid id, decimal value, LimitType limitType, AveragingWindow period,
        Guid permitId, Guid unitId, Guid pollutantId,
        Guid? emissionSourceId, Guid? installationId,
        DateTime validFrom, DateTime? validTo) =>
        new(id, value, limitType, period, permitId, unitId, pollutantId,
            emissionSourceId, installationId, validFrom, validTo);

    public void UpdateDetails(decimal value, LimitType limitType, AveragingWindow period, Guid unitId,
        Guid pollutantId, Guid? emissionSourceId, Guid? installationId,
        DateTime validFrom, DateTime? validTo)
    {
        if ((emissionSourceId is null) == (installationId is null))
        {
            throw new InvalidOperationException(
                "EmissionLimit must target exactly one of EmissionSource or Installation.");
        }

        Value = value;
        LimitType = limitType;
        Period = period;
        UnitId = unitId;
        PollutantId = pollutantId;
        EmissionSourceId = emissionSourceId;
        InstallationId = installationId;
        ValidFrom = validFrom;
        ValidTo = validTo;
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
                $"EnterpriseId is immutable on EmissionLimit (current: {EnterpriseId}, attempted: {enterpriseId}).");
        }
    }
}
