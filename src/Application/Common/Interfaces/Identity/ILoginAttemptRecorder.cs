using Domain.Entities.User;

namespace Application.Common.Interfaces.Identity;

/// <summary>
/// Captures one login attempt — success or failure — for the security audit trail. Resolves
/// IP / user-agent from the ambient HttpContext so callers only pass what they know about the
/// outcome. Persistence is committed in the recorder's own UoW save (decoupled from the login
/// handler so a recorder failure cannot abort a successful auth).
/// </summary>
public interface ILoginAttemptRecorder
{
    Task RecordAsync(
        Guid? userId,
        string emailAttempted,
        LoginMethod method,
        LoginOutcome outcome,
        CancellationToken cancellationToken);
}
