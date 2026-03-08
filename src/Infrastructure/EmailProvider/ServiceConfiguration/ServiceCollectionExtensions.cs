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
        
        services.AddTransient<IEmailService, EmailService>();
        
        return services;
    }
}