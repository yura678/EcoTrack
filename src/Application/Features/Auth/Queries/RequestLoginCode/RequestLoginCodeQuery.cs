using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Auth.Queries.RequestLoginCode;

public record RequestLoginCodeQuery(string Email)
    : IRequest<Either<UserException, bool>>, IValidatableModel<RequestLoginCodeQuery>
{
    public IValidator<RequestLoginCodeQuery> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RequestLoginCodeQuery> validator)
    {
        validator.RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(256);
        return validator;
    }
}
