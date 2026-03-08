using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviours;


public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception during {RequestName}: {Message}", typeof(TRequest).Name, e.Message);
            throw;
        }
    }
}