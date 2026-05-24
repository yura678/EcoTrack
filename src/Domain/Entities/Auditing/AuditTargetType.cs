namespace Domain.Entities.Auditing;

/// <summary>
/// What kind of entity the audited action targeted. Combined with TargetId in
/// <see cref="AdminAuditLog"/> it identifies the row's subject.
/// </summary>
public enum AuditTargetType
{
    User = 1,
    Role = 2,
    Membership = 3,
    Invitation = 4,
    Enterprise = 5
}
