using Application.Features.Users.Exceptions;
using Application.Models.Jwt;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Auth.Queries.LoginByCode;

public record LoginByCodeQuery(string Email, string Code)
    : IRequest<Either<UserException, AccessToken>>, IValidatableModel<LoginByCodeQuery>
{
    public IValidator<LoginByCodeQuery> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<LoginByCodeQuery> validator)
    {
        validator.RuleFor(c => c.Email).NotEmpty().EmailAddress();
        validator.RuleFor(c => c.Code).NotEmpty();
        return validator;
    }
}
