using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Auth.Commands.ConfirmEmail;

public record ConfirmEmailCommand(string Email, string Code)
    : IRequest<Either<UserException, bool>>, IValidatableModel<ConfirmEmailCommand>
{
    public IValidator<ConfirmEmailCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ConfirmEmailCommand> validator)
    {
        validator.RuleFor(c => c.Email)
            .NotEmpty()
            .EmailAddress();
        validator.RuleFor(c => c.Code)
            .NotEmpty();
        return validator;
    }
}
