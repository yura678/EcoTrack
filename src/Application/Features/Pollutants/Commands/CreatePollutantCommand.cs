using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using Domain.Entities.Monitoring;
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
    public required PollutantCategory Category { get; init; }
    public required PollutantMedia Media { get; init; }
    public required MeasureUnitDimension DefaultDimension { get; init; }
    public string? CasNumber { get; init; }
    public decimal? DefaultO2Reference { get; init; }
    public decimal? EprtrThresholdKgYear { get; init; }

    public IValidator<CreatePollutantCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreatePollutantCommand> validator)
    {
        validator.RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        validator.RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.Category)
            .IsInEnum();

        validator.RuleFor(x => x.Media)
            .IsInEnum();

        validator.RuleFor(x => x.DefaultDimension)
            .IsInEnum();

        validator.RuleFor(x => x.CasNumber!)
            .MaximumLength(20)
            .When(x => x.CasNumber is not null);

        validator.RuleFor(x => x.DefaultO2Reference!.Value)
            .InclusiveBetween(0, 21)
            .When(x => x.DefaultO2Reference.HasValue);

        validator.RuleFor(x => x.EprtrThresholdKgYear!.Value)
            .GreaterThan(0)
            .When(x => x.EprtrThresholdKgYear.HasValue);

        return validator;
    }
}
