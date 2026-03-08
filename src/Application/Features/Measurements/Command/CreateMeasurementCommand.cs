using Application.Features.Measurements.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Measurements.Command;

public class CreateMeasurementCommand : IRequest<Either<MeasurementException, Measurement>>,
    IValidatableModel<CreateMeasurementCommand>
{
    public required DateTime Timestamp { get; init; }
    public required Guid EmissionSourceId { get; init; }
    public required Guid PollutantId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid UnitId { get; init; }
    public required AveragingWindow Period { get; init; }
    public required decimal Value { get; init; }

    public IValidator<CreateMeasurementCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateMeasurementCommand> validator)
    {
        validator.RuleFor(x => x.Timestamp)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow);

        validator.RuleFor(x => x.EmissionSourceId)
            .NotEmpty();

        validator.RuleFor(x => x.PollutantId)
            .NotEmpty();

        validator.RuleFor(x => x.DeviceId)
            .NotEmpty();

        validator.RuleFor(x => x.UnitId)
            .NotEmpty();

        validator.RuleFor(x => x.Period)
            .IsInEnum();

        validator.RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0);

        return validator;
    }
}