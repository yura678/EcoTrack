using System.Text.RegularExpressions;
using Application.Features.Users.Commands.RegisterByInvitation;
using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.SendInvitation;

public record SendInvitationCommand(string Email, Guid RoleId)
    : IRequest<Either<UserException, string>>, IValidatableModel<SendInvitationCommand>
{
    public IValidator<SendInvitationCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<SendInvitationCommand> validator)
    {
        validator.RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.")
            .NotNull()
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email format is invalid");

        validator.RuleFor(c => c.RoleId)
            .NotEmpty();
        
        return validator;
    }
    }
