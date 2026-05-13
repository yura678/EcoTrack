using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Tests.Data.Enterprises;

public static class EmissionLimitsData
{
    public static EmissionLimit FirstTestLimit(
        Guid permitId,
        Guid unitId,
        Guid pollutantId,
        Guid emissionSourceId)
        => EmissionLimit.New(
            id: Guid.NewGuid(),
            value: 50m,
            limitType: LimitType.Concentration,
            period: AveragingWindow.Hour1,
            permitId: permitId,
            unitId: unitId,
            pollutantId: pollutantId,
            emissionSourceId: emissionSourceId,
            installationId: null,
            validFrom: DateTime.UtcNow.AddDays(-1),
            validTo: null);
}
