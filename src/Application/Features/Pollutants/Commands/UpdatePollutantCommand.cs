using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.Pollutants.Commands;

public class UpdatePollutantCommand : IRequest<Either<PollutantException, Pollutant>>,
    IValidatableModel<UpdatePollutantCommand>
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }

    public IValidator<UpdatePollutantCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdatePollutantCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        return validator;
    }
}