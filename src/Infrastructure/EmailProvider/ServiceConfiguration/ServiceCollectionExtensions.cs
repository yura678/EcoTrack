using Application.Common.Interfaces.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.EmailProvider.ServiceConfiguration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmailProviderServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MailSettings>(configuration.GetSection("MailSettings"));

        // Outbox writer — scoped so it shares the caller's ApplicationDbContext (and therefore
        // its transaction). The Pending row commits in the same tx as the caller's business
        // entities; the EmailOutboxDispatchInterceptor handles post-commit Hangfire dispatch.
        services.AddScoped<IEmailService, EmailService>();
        // Scoped per DbContext so its per-instance pending-id list is bounded to a single
        // unit-of-work. Registered separately and wired into AddDbContext below.
        services.AddScoped<EmailOutboxDispatchInterceptor>();
        // SMTP transport is only used by the Hangfire processor; transient is fine because
        // each invocation builds its own SmtpClient.
        services.AddTransient<SmtpEmailSender>();
        services.AddScoped<EmailOutboxProcessor>();
        services.AddScoped<EmailOutboxSweeper>();

        return services;
    }
}