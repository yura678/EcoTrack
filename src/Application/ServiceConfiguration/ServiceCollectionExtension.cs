using System.Reflection;
using Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application.ServiceConfiguration;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scan Application (commands/queries), Infrastructure (notification handlers like
        // EnqueueComplianceNotificationsHandler), and Api (SignalR broadcast handlers).
        // Without all three, INotificationHandler implementations outside the Application
        // assembly silently never fire.
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblies(
                Assembly.GetExecutingAssembly(),
                Assembly.Load("Infrastructure"),
                Assembly.Load("Api")));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidateCommandBehavior<,>));


        return services;
    }
}