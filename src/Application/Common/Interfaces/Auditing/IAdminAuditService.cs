using System.Text.Json;
using Domain.Entities.Auditing;

namespace Application.Common.Interfaces.Auditing;

/// <summary>
/// Captures admin-initiated actions in the audit log. Implementations resolve the actor's
/// identity (user id, email, role) and request context (IP, user-agent) from the ambient
/// HttpContext / ICurrentUserService so callers only need to describe what happened.
///
/// The write joins the current EF unit-of-work — the audit row is persisted in the same
/// transaction as the business state change. If the business write fails, no audit row is
/// committed; if the audit write fails, the whole operation aborts.
/// </summary>
public interface IAdminAuditService
{
    Task LogAsync(
        AuditAction action,
        AuditTargetType targetType,
        Guid? targetId,
        string? targetLabel,
        Guid? enterpriseId = null,
        JsonDocument? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// For flows where the actor is known but the request is unauthenticated (e.g. self-service
    /// password reset — token submission has no JWT). The caller supplies actor identity
    /// directly; HttpContext is still consulted for IP / user-agent.
    /// </summary>
    Task LogWithExplicitActorAsync(
        Guid actorUserId,
        string actorEmail,
        AuditAction action,
        AuditTargetType targetType,
        Guid? targetId,
        string? targetLabel,
        Guid? enterpriseId = null,
        JsonDocument? details = null,
        CancellationToken cancellationToken = default);
}
