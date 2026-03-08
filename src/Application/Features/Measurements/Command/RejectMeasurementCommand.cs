using Application.Features.Measurements.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.Measurements.Command;

public class RejectMeasurementCommand : IRequest<Either<MeasurementException, Measurement>>,
    IValidatableModel<RejectMeasurementCommand>
{
    public required Guid Id { get; init; }
    public required string Reason { get; init; }

    public IValidator<RejectMeasurementCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RejectMeasurementCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(1000);

        return validator;
    }
}