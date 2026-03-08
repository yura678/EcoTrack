namespace Application.Common.Interfaces.Persistence;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);
}