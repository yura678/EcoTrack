using System.Text.Json;
using Domain.Common;

namespace Domain.Entities.Auditing;

/// <summary>
/// Immutable record of an admin-initiated action. One row per action; never updated or deleted
/// once written. Snapshots actor + target identity strings so the row stays readable after the
/// linked user or role is renamed / removed.
///
/// EnterpriseId is nullable to leave room for genuinely global superAdmin actions (e.g.
/// editing a global role). Most rows are scoped to the affected tenant, even when performed
/// by a superAdmin against a tenant's user.
/// </summary>
public class AdminAuditLog : BaseEntity
{
    private AdminAuditLog() { }

    public Guid? EnterpriseId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    public Guid ActorUserId { get; private set; }
    public string ActorEmail { get; private set; } = null!;
    public string? ActorRole { get; private set; }

    public AuditAction Action { get; private set; }
    public AuditTargetType TargetType { get; private set; }
    public Guid? TargetId { get; private set; }
    public string? TargetLabel { get; private set; }

    /// <summary>
    /// jsonb payload with action-specific fields (e.g. {"oldRoleId":..., "newRoleId":...}).
    /// Use System.Text.Json so the writer can stay agnostic of EF / Npgsql plumbing.
    /// </summary>
    public JsonDocument? Details { get; private set; }

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public static AdminAuditLog Create(
        Guid? enterpriseId,
        Guid actorUserId,
        string actorEmail,
        string? actorRole,
        AuditAction action,
        AuditTargetType targetType,
        Guid? targetId,
        string? targetLabel,
        JsonDocument? details,
        string? ipAddress,
        string? userAgent)
    {
        return new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            EnterpriseId = enterpriseId,
            OccurredAt = DateTime.UtcNow,
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetLabel = targetLabel,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }
}
