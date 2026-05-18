using System.Collections.Concurrent;
using Application.Common.Interfaces.Persistence;

namespace Tests.Common;

public record SentEmail(string To, string Subject, string Body);

/// <summary>
/// Test double for IEmailService that stashes every send into an in-memory queue so tests
/// can assert on the recipient, subject, and body of emails the dispatcher emitted.
/// </summary>
public class RecordingEmailService : IEmailService
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        Sent.Enqueue(new SentEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }

    public void Clear() => Sent.Clear();
}
