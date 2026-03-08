using Domain.Entities.Enterprises;

namespace Tests.Data.Enterprises;

public static class PermitsData
{
    public static Permit FirstTestPermit(
        Guid permitId,
        Guid installationId,
        EmissionLimit[] limits)
    {
        var permit = Permit.New(
            id: permitId,
            installationId: installationId,
            number: "P-001",
            permitType: PermitType.Air,
            issuedAt: DateTime.UtcNow.AddDays(-30),
            validUntil: DateTime.UtcNow.AddYears(1),
            authority: "EcoInspectorate",
            notes: null,
            emissionLimits: limits);

        permit.ChangeStatus(PermitStatus.Active);
        return permit;
    }
}