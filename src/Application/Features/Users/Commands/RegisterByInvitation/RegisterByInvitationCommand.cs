using Application.Features.Users.Commands.Create;
using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Users.Commands.RegisterByInvitation;

public record RegisterByInvitationCommand(
    string Token,
    string Name,
    string FamilyName,
    string Email,
    string Password)
    : IRequest<Either<UserException, UserCreateCommandResult>>
        , IValidatableModel<RegisterByInvitationCommand>
{
    public IValidator<RegisterByInvitationCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RegisterByInvitationCommand> validator)
    {
        validator
            .RuleFor(c => c.Name)
            .NotEmpty()
            .NotNull()
            .WithMessage("User must have first name");

        validator
            .RuleFor(c => c.FamilyName)
            .NotEmpty()
            .NotNull()
            .WithMessage("User must have last name");

        validator.RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.")
            .NotNull()
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email format is invalid");

        validator.RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        return validator;
    }
}
