using Domain.Common;

namespace Domain.Entities.User;

/// <summary>
/// Immutable record of a login attempt — both successes and failures. UserId is nullable for
/// the "unknown email" outcome where we never resolved an account; EmailAttempted always
/// carries what the caller typed so security analysis can spot brute-force across non-existent
/// users.
/// </summary>
public class LoginAttempt : BaseEntity
{
    private LoginAttempt() { }

    public Guid? UserId { get; private set; }
    public string EmailAttempted { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public LoginMethod Method { get; private set; }
    public LoginOutcome Outcome { get; private set; }

    public static LoginAttempt Create(
        Guid? userId,
        string emailAttempted,
        string? ipAddress,
        string? userAgent,
        LoginMethod method,
        LoginOutcome outcome)
    {
        return new LoginAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EmailAttempted = emailAttempted,
            OccurredAt = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Method = method,
            Outcome = outcome
        };
    }
}

public enum LoginMethod
{
    Password = 0,
    EmailCode = 1
}

public enum LoginOutcome
{
    Success = 0,
    InvalidCredentials = 1,
    UserLocked = 2,
    EmailNotConfirmed = 3,
    UnknownEmail = 4,
    EnterpriseNotApproved = 5
}
