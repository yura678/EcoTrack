using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Profile.Commands.ChangeMyPassword;

public class ChangeMyPasswordCommand : IRequest<Either<UserException, bool>>,
    IValidatableModel<ChangeMyPasswordCommand>
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }

    public IValidator<ChangeMyPasswordCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ChangeMyPasswordCommand> validator)
    {
        validator.RuleFor(x => x.CurrentPassword).NotEmpty();
        validator.RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(6); // matches Identity default; tightened password policy lives in IdentityOptions
        return validator;
    }
}
