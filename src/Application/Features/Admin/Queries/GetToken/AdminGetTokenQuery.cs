using Application.Features.Admin.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Admin.Queries.GetToken;

public record AdminGetTokenQuery(string UserName, string Password) :
    IRequest<Either<AdminException, AdminGetTokenQueryResult>>,
    IValidatableModel<AdminGetTokenQuery>
{
    public IValidator<AdminGetTokenQuery> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<AdminGetTokenQuery> validator)
    {
        validator.RuleFor(c => c.UserName)
            .NotEmpty()
            .NotNull()
            .WithMessage("Please enter admin username");

        validator.RuleFor(c => c.Password)
            .NotEmpty()
            .NotNull()
            .WithMessage("Please enter admin password");

        return validator;
    }
};