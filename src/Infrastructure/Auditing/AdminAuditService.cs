using System.Security.Claims;
using System.Text.Json;
using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Repositories.Auditing;
using Domain.Entities.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Auditing;

internal class AdminAuditService(
    IAdminAuditLogRepository repository,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUserService,
    ILogger<AdminAuditService> logger) : IAdminAuditService
{
    public async Task LogAsync(
        AuditAction action,
        AuditTargetType targetType,
        Guid? targetId,
        string? targetLabel,
        Guid? enterpriseId = null,
        JsonDocument? details = null,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = currentUserService.GetCurrentUserId();
        if (actorUserId is null)
        {
            // System-context writes (background jobs, seed) bypass the audit log — there is no
            // real human actor to attribute the action to and recording "system" would dilute
            // the table's value. Log a warning so we notice if a request path slips through.
            logger.LogWarning(
                "Admin audit skipped: no actor user id in context (action={Action})", action);
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        var actorEmail = user?.FindFirst(ClaimTypes.Email)?.Value
                         ?? user?.Identity?.Name
                         ?? "<unknown>";
        var actorRole = user?.FindFirst(ClaimTypes.Role)?.Value;

        var resolvedEnterpriseId = enterpriseId ?? currentUserService.GetCurrentEnterpriseId();

        var entity = AdminAuditLog.Create(
            enterpriseId: resolvedEnterpriseId,
            actorUserId: actorUserId.Value,
            actorEmail: actorEmail,
            actorRole: actorRole,
            action: action,
            targetType: targetType,
            targetId: targetId,
            targetLabel: targetLabel,
            details: details,
            ipAddress: ResolveIpAddress(httpContext),
            userAgent: ResolveUserAgent(httpContext));

        await repository.AddAsync(entity, cancellationToken);
    }

    public async Task LogWithExplicitActorAsync(
        Guid actorUserId,
        string actorEmail,
        AuditAction action,
        AuditTargetType targetType,
        Guid? targetId,
        string? targetLabel,
        Guid? enterpriseId = null,
        JsonDocument? details = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var entity = AdminAuditLog.Create(
            enterpriseId: enterpriseId,
            actorUserId: actorUserId,
            actorEmail: actorEmail,
            actorRole: null, // unauthenticated path — no role claim to record
            action: action,
            targetType: targetType,
            targetId: targetId,
            targetLabel: targetLabel,
            details: details,
            ipAddress: ResolveIpAddress(httpContext),
            userAgent: ResolveUserAgent(httpContext));

        await repository.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Resolves the originating IP, honouring X-Forwarded-For when present (first hop wins).
    /// Keep in mind that the proxy must be trusted — ASP.NET's ForwardedHeadersMiddleware should
    /// be wired in production so RemoteIpAddress already reflects the right value.
    /// </summary>
    private static string? ResolveIpAddress(HttpContext? httpContext)
    {
        if (httpContext is null) return null;
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var firstHop = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstHop)) return firstHop;
        }
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ResolveUserAgent(HttpContext? httpContext)
    {
        if (httpContext is null) return null;
        var ua = httpContext.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? null : ua;
    }
}
