using Application.Common.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Infrastructure.Persistence;

namespace Infrastructure.EmailProvider;

/// <summary>
/// Public <see cref="IEmailService"/> entry point — transactional outbox writer. Stages the
/// email row in the caller's <c>ApplicationDbContext</c> change tracker and lets the caller's
/// own <c>SaveChanges</c> commit it in the same transaction as its business entities. The row's
/// post-commit dispatch to the Hangfire processor is handled by
/// <see cref="EmailOutboxDispatchInterceptor"/>, so a rollback leaves no orphan row and no
/// orphan Hangfire job.
/// </summary>
public class EmailService(ApplicationDbContext context) : IEmailService
{
    public async Task SendEmailAsync(string toEmail, string subject, string body,
        CancellationToken cancellationToken = default)
    {
        var row = EmailOutbox.NewPending(Guid.NewGuid(), toEmail, subject, body);
        await context.Set<EmailOutbox>().AddAsync(row, cancellationToken);
        // Intentionally no SaveChanges here — caller controls the unit-of-work and commits
        // this row alongside its business state.
    }
}
