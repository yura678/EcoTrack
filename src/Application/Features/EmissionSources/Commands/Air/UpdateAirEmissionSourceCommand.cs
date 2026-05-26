using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.EmissionSources.Commands.Air;

public class UpdateAirEmissionSourceCommand : IRequest<Either<EmissionSourceException, EmissionSource>>,
    IValidatableModel<UpdateAirEmissionSourceCommand>
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required double Height { get; init; }
    public required double Diameter { get; init; }
    public required double DesignFlowRate { get; init; }

    public IValidator<UpdateAirEmissionSourceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateAirEmissionSourceCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        validator.RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90);

        validator.RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180);

        validator.RuleFor(x => x.Height)
            .NotEmpty();

        validator.RuleFor(x => x.Diameter)
            .NotEmpty();

        validator.RuleFor(x => x.DesignFlowRate)
            .NotEmpty();

        return validator;
    }
}
