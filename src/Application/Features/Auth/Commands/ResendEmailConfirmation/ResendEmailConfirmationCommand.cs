using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Auth.Commands.ResendEmailConfirmation;

public record ResendEmailConfirmationCommand(string Email)
    : IRequest<Either<UserException, bool>>, IValidatableModel<ResendEmailConfirmationCommand>
{
    public IValidator<ResendEmailConfirmationCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ResendEmailConfirmationCommand> validator)
    {
        validator.RuleFor(c => c.Email).NotEmpty().EmailAddress();
        return validator;
    }
}
