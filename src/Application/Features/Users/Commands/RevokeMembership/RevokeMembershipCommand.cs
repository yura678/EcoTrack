using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.RevokeMembership;

public record RevokeMembershipCommand(Guid UserId)
    : IRequest<Either<UserException, bool>>, IValidatableModel<RevokeMembershipCommand>
{
    public IValidator<RevokeMembershipCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RevokeMembershipCommand> validator)
    {
        validator.RuleFor(c => c.UserId).NotEmpty();
        return validator;
    }
}
