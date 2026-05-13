using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.CalibrationRecords;

public class UpdateCalibrationRecordDtoValidator : AbstractValidator<UpdateCalibrationRecordDto>
{
    public UpdateCalibrationRecordDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Result).IsInEnum();
        RuleFor(x => x.PerformedBy).NotEmpty().MaximumLength(255);
        RuleFor(x => x.NextDueAt)
            .GreaterThan(x => x.PerformedAt)
            .WithMessage("NextDueAt must be after PerformedAt.");
        RuleFor(x => x.CertificateNumber!).MaximumLength(100)
            .When(x => x.CertificateNumber is not null);
        RuleFor(x => x.Notes!).MaximumLength(1000)
            .When(x => x.Notes is not null);
    }
}
