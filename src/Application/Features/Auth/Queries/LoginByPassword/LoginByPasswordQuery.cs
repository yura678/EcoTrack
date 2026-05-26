using Application.Features.Users.Exceptions;
using Application.Models.Auth;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Auth.Queries.LoginByPassword;

public record LoginByPasswordQuery(string Email, string Password)
    : IRequest<Either<UserException, AuthSession>>, IValidatableModel<LoginByPasswordQuery>
{
    public IValidator<LoginByPasswordQuery> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<LoginByPasswordQuery> validator)
    {
        validator.RuleFor(c => c.Email).NotEmpty().EmailAddress();
        validator.RuleFor(c => c.Password).NotEmpty();
        return validator;
    }
}
