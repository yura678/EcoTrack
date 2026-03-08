using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Pollutants.Commands;

public class DeletePollutantCommand : IRequest<Either<PollutantException, Pollutant>>,
    IValidatableModel<DeletePollutantCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeletePollutantCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeletePollutantCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}