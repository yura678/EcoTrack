using Application.Features.Users.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Users.Commands.RevokeMembership;

// EnterpriseId is optional: tenant admins leave it null and the handler derives it from
// the JWT's CompanyId claim. SuperAdmin must pass it explicitly to target a tenant.
public record RevokeMembershipCommand(Guid UserId, Guid? EnterpriseId = null)
    : IRequest<Either<UserException, bool>>, IValidatableModel<RevokeMembershipCommand>
{
    public IValidator<RevokeMembershipCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RevokeMembershipCommand> validator)
    {
        validator.RuleFor(c => c.UserId).NotEmpty();
        return validator;
    }
}
