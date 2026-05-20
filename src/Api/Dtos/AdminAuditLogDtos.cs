using System.Text.Json;
using Domain.Entities.Auditing;

namespace Api.Dtos;

public record AdminAuditLogQueryDto(
    AuditAction? Action,
    AuditTargetType? TargetType,
    Guid? TargetId,
    Guid? ActorUserId,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Wire representation of an audit log row. Details is exposed as JsonElement so the JSON
/// stays as JSON in the HTTP response without an intermediate string-escape step.
/// </summary>
public record AdminAuditLogDto(
    Guid Id,
    Guid? EnterpriseId,
    DateTime OccurredAt,
    Guid ActorUserId,
    string ActorEmail,
    string? ActorRole,
    AuditAction Action,
    AuditTargetType TargetType,
    Guid? TargetId,
    string? TargetLabel,
    JsonElement? Details,
    string? IpAddress,
    string? UserAgent)
{
    public static AdminAuditLogDto FromDomainModel(AdminAuditLog e) =>
        new(e.Id, e.EnterpriseId, e.OccurredAt, e.ActorUserId, e.ActorEmail, e.ActorRole,
            e.Action, e.TargetType, e.TargetId, e.TargetLabel,
            e.Details?.RootElement, e.IpAddress, e.UserAgent);
}
