using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.ChangeMemberRole;

public record ChangeMemberRoleCommand(Guid UserId, Guid RoleId)
    : IRequest<Either<UserException, bool>>, IValidatableModel<ChangeMemberRoleCommand>
{
    public IValidator<ChangeMemberRoleCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ChangeMemberRoleCommand> validator)
    {
        validator.RuleFor(c => c.UserId).NotEmpty();
        validator.RuleFor(c => c.RoleId).NotEmpty();
        return validator;
    }
}
