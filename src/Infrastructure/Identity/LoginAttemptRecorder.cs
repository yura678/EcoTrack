using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Repositories;
using Domain.Entities.User;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Identity;

internal class LoginAttemptRecorder(
    ILoginAttemptRepository repository,
    IHttpContextAccessor httpContextAccessor) : ILoginAttemptRecorder
{
    /// <summary>
    /// Stages a <see cref="LoginAttempt"/> row in the caller's unit-of-work. Does NOT save —
    /// the caller is responsible for calling <c>SaveChangesAsync</c> so every persistence
    /// boundary is visible at the handler level. Combine with any Identity mutations
    /// (IncrementAccessFailed, ResetLockout, UpdateSecurityStamp, …) in a single SaveChanges
    /// so the audit row + auth side-effect commit atomically.
    /// </summary>
    public async Task RecordAsync(
        Guid? userId,
        string emailAttempted,
        LoginMethod method,
        LoginOutcome outcome,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var attempt = LoginAttempt.Create(
            userId,
            emailAttempted,
            ResolveIpAddress(httpContext),
            ResolveUserAgent(httpContext),
            method,
            outcome);

        await repository.AddAsync(attempt, cancellationToken);
    }

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
