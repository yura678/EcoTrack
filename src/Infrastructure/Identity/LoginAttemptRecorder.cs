using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Repositories;
using Domain.Entities.User;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity;

internal class LoginAttemptRecorder(
    ILoginAttemptRepository repository,
    IUnitOfWork unitOfWork,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LoginAttemptRecorder> logger) : ILoginAttemptRecorder
{
    public async Task RecordAsync(
        Guid? userId,
        string emailAttempted,
        LoginMethod method,
        LoginOutcome outcome,
        CancellationToken cancellationToken)
    {
        try
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
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Recording a login attempt must never break the auth flow. If the DB write fails
            // we log a warning so security review can spot dropped events but the caller still
            // gets the auth result they earned.
            logger.LogWarning(ex,
                "Failed to record login attempt for {Email} (outcome={Outcome})",
                emailAttempted, outcome);
        }
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
