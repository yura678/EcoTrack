using Application.Features.Role.Exceptions;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Role.Commands.DeleteRole;

public record DeleteRoleCommand(Guid RoleId)
    : IRequest<Either<RoleException, bool>>, IValidatableModel<DeleteRoleCommand>
{
    public IValidator<DeleteRoleCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteRoleCommand> validator)
    {
        validator.RuleFor(c => c.RoleId).NotEmpty();
        return validator;
    }
}
