using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Permits.Commands;

public class ArchivePermitCommand : IRequest<Either<PermitException, Permit>>,
    IValidatableModel<ArchivePermitCommand>
{
    public required Guid Id { get; init; }

    public IValidator<ArchivePermitCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ArchivePermitCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}