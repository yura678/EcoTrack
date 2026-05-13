using Application.Features.CalibrationRecords.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.CalibrationRecords.Commands;

public class UpdateCalibrationRecordCommand
    : IRequest<Either<CalibrationRecordException, CalibrationRecord>>,
        IValidatableModel<UpdateCalibrationRecordCommand>
{
    public required Guid Id { get; init; }
    public required CalibrationType Type { get; init; }
    public required DateTime PerformedAt { get; init; }
    public required DateTime NextDueAt { get; init; }
    public required CalibrationResult Result { get; init; }
    public required string PerformedBy { get; init; }
    public string? CertificateNumber { get; init; }
    public string? Notes { get; init; }

    public IValidator<UpdateCalibrationRecordCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateCalibrationRecordCommand> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.Type).IsInEnum();
        validator.RuleFor(x => x.Result).IsInEnum();
        validator.RuleFor(x => x.PerformedBy).NotEmpty().MaximumLength(255);
        validator.RuleFor(x => x.NextDueAt)
            .GreaterThan(x => x.PerformedAt)
            .WithMessage("NextDueAt must be after PerformedAt.");
        validator.RuleFor(x => x.CertificateNumber!).MaximumLength(100)
            .When(x => x.CertificateNumber is not null);
        validator.RuleFor(x => x.Notes!).MaximumLength(1000)
            .When(x => x.Notes is not null);
        return validator;
    }
}
