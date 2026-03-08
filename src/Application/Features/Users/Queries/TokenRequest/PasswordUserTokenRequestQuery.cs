using Application.Features.Users.Exceptions;
using Application.Models.Common;
using Application.Models.Jwt;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Queries.TokenRequest;

public record PasswordUserTokenRequestQuery(string UserName, string Password)
    : IValidatableModel<PasswordUserTokenRequestQuery>, IRequest<Either<UserException, AccessToken>>
{
    public IValidator<PasswordUserTokenRequestQuery> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<PasswordUserTokenRequestQuery> validator)
    {
        validator.RuleFor(c => c.UserName)
            .NotEmpty();

        validator.RuleFor(c => c.Password)
            .NotEmpty();

        return validator;
    }
}