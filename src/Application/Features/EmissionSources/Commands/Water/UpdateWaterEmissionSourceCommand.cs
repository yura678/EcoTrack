using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.EmissionSources.Commands.Water;

public class UpdateWaterEmissionSourceCommand : IRequest<Either<EmissionSourceException, EmissionSource>>,
    IValidatableModel<UpdateWaterEmissionSourceCommand>
{
    public required Guid Id { get; init; }
    public required string Receiver { get; init; }
    public required double DesignFlowRate { get; init; }

    public IValidator<UpdateWaterEmissionSourceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateWaterEmissionSourceCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();
        
        validator.RuleFor(x => x.Receiver)
            .NotEmpty()
            .MaximumLength(255);
        
        validator.RuleFor(x => x.DesignFlowRate)
            .NotEmpty();

        return validator;
    }
}