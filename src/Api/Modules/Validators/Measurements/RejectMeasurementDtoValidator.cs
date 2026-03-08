using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Measurements;

public class RejectMeasurementDtoValidator : AbstractValidator<RejectMeasurementDto>
{
    public RejectMeasurementDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(1000);
    }
}