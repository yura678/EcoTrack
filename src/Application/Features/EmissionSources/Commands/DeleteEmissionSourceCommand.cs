using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.EmissionSources.Commands;

public class DeleteEmissionSourceCommand : IRequest<Either<EmissionSourceException, EmissionSource>>,
    IValidatableModel<DeleteEmissionSourceCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteEmissionSourceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteEmissionSourceCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}