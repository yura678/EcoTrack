using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Permits.Commands;

public class ActivatePermitCommand : IRequest<Either<PermitException, Permit>>,
    IValidatableModel<ActivatePermitCommand>
{
    public required Guid Id { get; init; }

    public IValidator<ActivatePermitCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ActivatePermitCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}