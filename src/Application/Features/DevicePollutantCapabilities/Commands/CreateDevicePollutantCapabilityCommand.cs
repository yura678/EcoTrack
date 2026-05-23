using Application.Features.DevicePollutantCapabilities.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.DevicePollutantCapabilities.Commands;

public class CreateDevicePollutantCapabilityCommand
    : IRequest<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>,
        IValidatableModel<CreateDevicePollutantCapabilityCommand>
{
    public required Guid DeviceId { get; init; }
    public required Guid PollutantId { get; init; }
    public required decimal RangeMin { get; init; }
    public required decimal RangeMax { get; init; }
    public required Guid RangeUnitId { get; init; }
    public string? AccuracyClass { get; init; }
    public int ExpectedIntervalMinutes { get; init; } = 1;

    public IValidator<CreateDevicePollutantCapabilityCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateDevicePollutantCapabilityCommand> validator)
    {
        validator.RuleFor(x => x.DeviceId).NotEmpty();
        validator.RuleFor(x => x.PollutantId).NotEmpty();
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
