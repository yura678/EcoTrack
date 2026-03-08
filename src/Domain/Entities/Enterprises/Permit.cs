using Domain.Common;

namespace Domain.Entities.Enterprises;

public class Permit : BaseEntity
{
    public Guid InstallationId { get; private set; }
    public Installation? Installation { get; private set; }

    public string Number { get; private set; }
    public PermitType PermitType { get; private set; }
    public PermitStatus PermitStatus { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime ValidUntil { get; private set; }
    public string Authority { get; private set; }
    public string? Notes { get; private set; }

    public DateTime CreatedAt { get; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<EmissionLimit>? EmissionLimits { get; private set; } = [];

    private Permit(Guid id, Guid installationId, string number, PermitType permitType,
        PermitStatus permitStatus, DateTime issuedAt, DateTime validUntil, string authority, string? notes,
        DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        InstallationId = installationId;
        Number = number;
        PermitType = permitType;
        PermitStatus = permitStatus;
        IssuedAt = issuedAt;
        ValidUntil = validUntil;
        Authority = authority;
        Notes = notes;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Permit New(Guid id, Guid installationId, string number,
        PermitType permitType, DateTime issuedAt, DateTime validUntil, string authority,
        string? notes, ICollection<EmissionLimit> emissionLimits) =>
        new(id, installationId, number, permitType, PermitStatus.Draft, issuedAt, validUntil, authority, notes,
            DateTime.UtcNow, null)
        {
            EmissionLimits = emissionLimits
        };


    public void UpdateDetails(string number, PermitType permitType, DateTime issuedAt, DateTime validUntil,
        string authority, string? notes)
    {
        Number = number;
        PermitType = permitType;
        IssuedAt = issuedAt;
        ValidUntil = validUntil;
        Authority = authority;
        Notes = notes;

        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(PermitStatus status)
    {
        PermitStatus = status;

        UpdatedAt = DateTime.UtcNow;
    }
}

public enum PermitStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
    Revoked = 3
}

public enum PermitType
{
    Integrated,
    Air,
    Water
}