using Application.Features.DevicePollutantCapabilities.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.DevicePollutantCapabilities.Commands;

public class UpdateDevicePollutantCapabilityCommand
    : IRequest<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>,
        IValidatableModel<UpdateDevicePollutantCapabilityCommand>
{
    public required Guid Id { get; init; }
    public required decimal RangeMin { get; init; }
    public required decimal RangeMax { get; init; }
    public required Guid RangeUnitId { get; init; }
    public string? AccuracyClass { get; init; }
    public int ExpectedIntervalMinutes { get; init; } = 1;

    public IValidator<UpdateDevicePollutantCapabilityCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateDevicePollutantCapabilityCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.RangeUnitId).NotEmpty();
        validator.RuleFor(x => x.RangeMax)
            .GreaterThan(x => x.RangeMin)
            .WithMessage("RangeMax must be greater than RangeMin.");
        validator.RuleFor(x => x.AccuracyClass!).MaximumLength(50)
            .When(x => x.AccuracyClass is not null);
        validator.RuleFor(x => x.ExpectedIntervalMinutes).GreaterThanOrEqualTo(1)
            .WithMessage("ExpectedIntervalMinutes must be at least 1.");

        return validator;
    }
}
