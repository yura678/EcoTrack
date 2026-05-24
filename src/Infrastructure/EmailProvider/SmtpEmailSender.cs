using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.EmailProvider;

/// <summary>
/// Pure SMTP transport — the body that used to live in the old <see cref="EmailService"/>.
/// Lifted out so the public <c>IEmailService</c> entry point can be the outbox writer and the
/// actual network call lives behind a Hangfire-driven background worker.
/// </summary>
public class SmtpEmailSender(IOptions<MailSettings> mailSettings, ILogger<SmtpEmailSender> logger)
{
    private readonly MailSettings _mailSettings = mailSettings.Value;

    public virtual async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        var email = new MimeMessage();
        email.Sender = MailboxAddress.Parse(_mailSettings.From);
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;
        email.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(_mailSettings.UserName, _mailSettings.Password, ct);
            await smtp.SendAsync(email, ct);
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP send failed for {ToEmail}", toEmail);
            throw;
        }
    }
}
