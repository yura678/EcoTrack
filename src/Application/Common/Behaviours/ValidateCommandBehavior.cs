using Application.Models.Common;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Application.Common.Behaviours;

public class ValidateCommandBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : IOperationResult, new()
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest message, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationFailure>();


        foreach (var validator in validators)
        {
            var validationResult =
                await validator.ValidateAsync(new ValidationContext<TRequest>(message), cancellationToken);

            if (!validationResult.IsValid)
                errors.AddRange(validationResult.Errors);
        }

        if (errors.Any())
        {
            return new TResponse()
            {
                ErrorMessages = errors.Select(c => new KeyValuePair<string, string>(c.PropertyName, c.ErrorMessage))
                    .ToList()
            };
        }


        return await next(cancellationToken);
    }
}