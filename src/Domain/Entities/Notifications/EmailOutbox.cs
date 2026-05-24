using Domain.Common;

namespace Domain.Entities.Notifications;

public enum EmailOutboxStatus
{
    Pending = 0,
    Sent = 1,
    DeadLettered = 2
}

/// <summary>
/// Outbox row for an outbound email. All callers of <see cref="Application.Common.Interfaces.Persistence.IEmailService"/>
/// produce one of these in their own transaction-of-record. A background Hangfire processor picks
/// up Pending rows and pushes them through SMTP, with the row updated to Sent or — after retries
/// are exhausted — DeadLettered. The pattern:
/// <list type="bullet">
///   <item>HTTP endpoints don't block on SMTP latency.</item>
///   <item>Anonymous flows (forgot password, login code) don't leak existence via 5xx-vs-200 because
///   the only failure that can reach the caller is a DB write, not an SMTP timeout.</item>
///   <item>Failures get a persistent audit trail in our DB instead of vanishing into log files.</item>
/// </list>
/// </summary>
public class EmailOutbox : BaseEntity
{
    public string ToEmail { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    public EmailOutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? FirstAttemptedAt { get; private set; }
    public DateTime? LastAttemptedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    private EmailOutbox() { }

    public static EmailOutbox NewPending(Guid id, string toEmail, string subject, string body) =>
        new()
        {
            Id = id,
            ToEmail = toEmail,
            Subject = subject,
            Body = body,
            Status = EmailOutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

    public void MarkAttempt(string? error)
    {
        AttemptCount++;
        LastError = error;
        var now = DateTime.UtcNow;
        FirstAttemptedAt ??= now;
        LastAttemptedAt = now;
    }

    public void MarkSent()
    {
        Status = EmailOutboxStatus.Sent;
        SentAt = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkDeadLettered(string finalError)
    {
        Status = EmailOutboxStatus.DeadLettered;
        LastError = finalError;
        LastAttemptedAt = DateTime.UtcNow;
    }
}
