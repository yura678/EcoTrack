using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Measurements;

public class CreateMeasurementDtoValidator : AbstractValidator<CreateMeasurementDto>
{
    public CreateMeasurementDtoValidator()
    {
        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow);
        
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        RuleFor(x => x.PollutantId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();

        RuleFor(x => x.Period).IsInEnum();

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0);
        
    }
}