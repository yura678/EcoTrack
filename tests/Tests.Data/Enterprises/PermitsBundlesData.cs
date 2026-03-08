using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;

namespace Tests.Data.Enterprises;

public static class PermitsBundlesData
{
    public static (Permit Permit, EmissionLimit[] Limits) FirstMultiLimitPermit(
        Guid installationId,
        Guid pollutantId,
        Guid sourceId,
        MeasureUnit mg,
        MeasureUnit g,
        MeasureUnit ug
    )
    {
        var permitId = Guid.NewGuid();

        var limit1 = EmissionLimit.New(
            Guid.NewGuid(),
            50m,
            AveragingWindow.OneHour,
            permitId,
            mg.Id,
            pollutantId,
            sourceId,
            DateTime.UtcNow.AddDays(-1),
            null);

        var limit2 = EmissionLimit.New(
            Guid.NewGuid(),
            0.05m,
            AveragingWindow.OneHour,
            permitId,
            g.Id,
            pollutantId,
            sourceId,
            DateTime.UtcNow.AddDays(-1),
            null);

        var limit3 = EmissionLimit.New(
            Guid.NewGuid(),
            50_000m,
            AveragingWindow.OneHour,
            permitId,
            ug.Id,
            pollutantId,
            sourceId,
            DateTime.UtcNow.AddDays(-1),
            null);

        var permit = Permit.New(
            permitId,
            installationId,
            "P-MULTI",
            PermitType.Air,
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddYears(1),
            "Inspectorate",
            null,
            [limit1, limit2, limit3]
        );

        permit.ChangeStatus(PermitStatus.Active);

        return (permit, [limit1, limit2, limit3]);
    }

    public static (Permit Permit, EmissionLimit[] Limits) DraftBundle(
        Guid installationId,
        Guid sourceId,
        Guid pollutantId,
        Guid unitId)
    {
        var permit = Permit.New(
            Guid.NewGuid(),
            installationId,
            number: "D-001",
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddYears(-1),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Draft Auth",
            notes: "notes",
            emissionLimits: []
        );

        var limit = EmissionLimit.New(
            Guid.NewGuid(),
            100,
            AveragingWindow.TwentyFourHours,
            permit.Id,
            unitId,
            pollutantId,
            sourceId,
            validFrom: DateTime.UtcNow.AddYears(-1),
            validTo: DateTime.UtcNow.AddYears(1));

        permit.EmissionLimits!.Add(limit);

        return (permit, [limit]);
    }

    public static (Permit Permit, EmissionLimit[] Limits) ActiveBundle(
        Guid installationId,
        Guid sourceId,
        Guid pollutantId,
        Guid unitId)
    {
        var permit = Permit.New(
            Guid.NewGuid(),
            installationId,
            number: "D-002",
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddYears(-1),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Draft Auth",
            notes: "notes",
            emissionLimits: []
        );

        permit.ChangeStatus(PermitStatus.Active);

        var limit = EmissionLimit.New(
            Guid.NewGuid(),
            100,
            AveragingWindow.TwentyFourHours,
            permit.Id,
            unitId,
            pollutantId,
            sourceId,
            validFrom: DateTime.UtcNow.AddYears(-1),
            validTo: DateTime.UtcNow.AddYears(1));

        permit.EmissionLimits!.Add(limit);

        return (permit, [limit]);
    }

    public static (Permit Permit, EmissionLimit[] Limits) ArchivedBundle(
        Guid installationId,
        Guid sourceId,
        Guid pollutantId,
        Guid unitId)
    {
        var permit = Permit.New(
            Guid.NewGuid(),
            installationId,
            number: "D-003",
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddYears(-1),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "Draft Auth",
            notes: "notes",
            emissionLimits: []
        );

        permit.ChangeStatus(PermitStatus.Archived);

        var limit = EmissionLimit.New(
            Guid.NewGuid(),
            100,
            AveragingWindow.TwentyFourHours,
            permit.Id,
            unitId,
            pollutantId,
            sourceId,
            validFrom: DateTime.UtcNow.AddYears(-1),
            validTo: DateTime.UtcNow.AddYears(1));

        permit.EmissionLimits!.Add(limit);
        return (permit, [limit]);
    }
}