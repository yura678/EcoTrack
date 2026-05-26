using Application.Features.Users.Exceptions;
using Application.Models.Auth;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.RefreshUserTokenCommand;

public record RefreshUserTokenCommand(Guid RefreshToken) : IRequest<Either<UserException, AuthSession>>,
    IValidatableModel<RefreshUserTokenCommand>
{
    public IValidator<RefreshUserTokenCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RefreshUserTokenCommand> validator)
    {
        validator.RuleFor(c => c.RefreshToken)
            .NotEmpty()
            .NotNull()
            .WithMessage("Please enter valid user refresh token");

        return validator;
    }
};