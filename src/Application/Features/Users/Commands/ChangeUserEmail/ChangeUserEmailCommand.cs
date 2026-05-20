using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.ChangeUserEmail;

public class ChangeUserEmailCommand : IRequest<Either<UserException, bool>>,
    IValidatableModel<ChangeUserEmailCommand>
{
    public required Guid UserId { get; init; }
    public required string NewEmail { get; init; }

    public IValidator<ChangeUserEmailCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ChangeUserEmailCommand> validator)
    {
        validator.RuleFor(x => x.UserId).NotEmpty();
        validator.RuleFor(x => x.NewEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320); // RFC 5321 maximum
        return validator;
    }
}
