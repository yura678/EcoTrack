using Application.Features.Auth.Exceptions;
using Application.Models.Auth;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Auth.Commands.SwitchEnterprise;

public class SwitchEnterpriseCommand : IRequest<Either<AuthException, AuthSession>>,
    IValidatableModel<SwitchEnterpriseCommand>
{
    public required Guid EnterpriseId { get; init; }

    public IValidator<SwitchEnterpriseCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<SwitchEnterpriseCommand> validator)
    {
        validator.RuleFor(x => x.EnterpriseId).NotEmpty();
        return validator;
    }
}
