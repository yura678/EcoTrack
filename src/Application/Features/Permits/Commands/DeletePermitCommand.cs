using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Permits.Commands;

public class DeletePermitCommand : IRequest<Either<PermitException, Permit>>,
    IValidatableModel<DeletePermitCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeletePermitCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeletePermitCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}