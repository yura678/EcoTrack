using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Permits.Commands;

public class RevokePermitCommand : IRequest<Either<PermitException, Permit>>,
    IValidatableModel<RevokePermitCommand>
{
    public required Guid Id { get; init; }

    public IValidator<RevokePermitCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RevokePermitCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}