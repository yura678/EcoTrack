using System.Text.RegularExpressions;
using Application.Features.Users.Exceptions;
using Application.Models.Common;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Queries.TokenRequest;

public record UserTokenRequestQuery(string Email)
    : IRequest<Either<UserException, UserTokenRequestQueryResponse>>,
        IValidatableModel<UserTokenRequestQuery>
{
    public IValidator<UserTokenRequestQuery> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UserTokenRequestQuery> validator)
    {
        validator.RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.")
            .NotNull()
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email format is invalid");


        return validator;
    }
};