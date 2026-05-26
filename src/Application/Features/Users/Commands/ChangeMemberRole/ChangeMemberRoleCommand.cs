using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.ChangeMemberRole;

// EnterpriseId is optional: tenant admins leave it null (handler derives it from the JWT's
// CompanyId claim). SuperAdmin must pass it explicitly because their session has no
// CompanyId — without it the handler can't tell which tenant the role change targets.
public record ChangeMemberRoleCommand(Guid UserId, Guid RoleId, Guid? EnterpriseId = null)
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
