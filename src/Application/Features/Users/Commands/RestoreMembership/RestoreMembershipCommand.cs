using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.RestoreMembership;

// EnterpriseId is optional: tenant admins leave it null and the handler derives it from
// the JWT's CompanyId claim. SuperAdmin must pass it explicitly to target a tenant —
// same contract as RevokeMembershipCommand.
public record RestoreMembershipCommand(Guid UserId, Guid? EnterpriseId = null)
    : IRequest<Either<UserException, bool>>, IValidatableModel<RestoreMembershipCommand>
{
    public IValidator<RestoreMembershipCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RestoreMembershipCommand> validator)
    {
        validator.RuleFor(c => c.UserId).NotEmpty();
        return validator;
    }
}
