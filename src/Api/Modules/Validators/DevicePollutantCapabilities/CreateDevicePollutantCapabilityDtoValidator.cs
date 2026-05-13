using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.DevicePollutantCapabilities;

public class CreateDevicePollutantCapabilityDtoValidator
    : AbstractValidator<CreateDevicePollutantCapabilityDto>
{
    public CreateDevicePollutantCapabilityDtoValidator()
    {
        RuleFor(x => x.PollutantId).NotEmpty();
        RuleFor(x => x.RangeUnitId).NotEmpty();
        RuleFor(x => x.RangeMax)
            .GreaterThan(x => x.RangeMin)
            .WithMessage("RangeMax must be greater than RangeMin.");
        RuleFor(x => x.AccuracyClass!).MaximumLength(50)
            .When(x => x.AccuracyClass is not null);
    }
}
