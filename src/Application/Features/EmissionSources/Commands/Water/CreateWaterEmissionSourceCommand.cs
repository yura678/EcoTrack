using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.EmissionSources.Commands.Water;

public class CreateWaterEmissionSourceCommand : IRequest<Either<EmissionSourceException, EmissionSource>>,
    IValidatableModel<CreateWaterEmissionSourceCommand>
{
    public required string Receiver { get; init; }
    public required double DesignFlowRate { get; init; }
    public required string Code { get; init; }
    public required Guid InstallationId { get; init; }

    public IValidator<CreateWaterEmissionSourceCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateWaterEmissionSourceCommand> validator)
    {
        validator.RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        validator.RuleFor(x => x.InstallationId)
            .NotEmpty();

        validator.RuleFor(x => x.Receiver)
            .NotEmpty()
            .MaximumLength(255);

        validator.RuleFor(x => x.DesignFlowRate)
            .NotEmpty();

        return validator;
    }
}