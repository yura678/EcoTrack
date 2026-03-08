using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Pollutants.Commands;

public class CreatePollutantCommand : IRequest<Either<PollutantException, Pollutant>>,
    IValidatableModel<CreatePollutantCommand>
{
    public required string Code { get; init; }
    public required string Name { get; init; }

    public IValidator<CreatePollutantCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreatePollutantCommand> validator)
    {
        validator.RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        return validator;
    }
}