using Domain.Entities.Notifications;
using Hangfire;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.EmailProvider;

/// <summary>
/// Hangfire job that picks up one <see cref="EmailOutbox"/> row by id, calls SMTP, and updates
/// the row. AutomaticRetry handles transient failures; once the retry budget is exhausted the
/// row stays Pending with a high <c>AttemptCount</c> and the last error — operators triage it
/// from the email_outbox table (a future Hangfire filter can flip terminal failures to
/// <c>DeadLettered</c> automatically; for now the audit trail is the AttemptCount column).
/// </summary>
public class EmailOutboxProcessor(
    ApplicationDbContext dbContext,
    SmtpEmailSender smtp,
    ILogger<EmailOutboxProcessor> logger)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 30, 120, 600, 1800, 3600 })]
    public async Task ProcessAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Set<EmailOutbox>()
            .FirstOrDefaultAsync(x => x.Id == outboxId, cancellationToken);
        if (row is null)
        {
            logger.LogWarning("EmailOutbox row {OutboxId} not found — skipping processor.", outboxId);
            return;
        }
        if (row.Status != EmailOutboxStatus.Pending)
        {
            // Idempotency: a Sent row should never be re-sent if Hangfire happens to re-run.
            return;
        }

        try
        {
            await smtp.SendAsync(row.ToEmail, row.Subject, row.Body, cancellationToken);
            row.MarkSent();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            row.MarkAttempt(Truncate(ex.Message, 2000));
            await dbContext.SaveChangesAsync(cancellationToken);
            // Re-throw so Hangfire treats this as a failure and applies the retry schedule;
            // OnAttemptsExhausted will flip the row to DeadLettered when the budget is spent.
            throw;
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
