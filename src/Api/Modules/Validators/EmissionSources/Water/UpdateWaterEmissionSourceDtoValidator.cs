using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.EmissionSources.Water;

public class UpdateWaterEmissionSourceDtoValidator : AbstractValidator<UpdateWaterEmissionSourceDto>
{
    public UpdateWaterEmissionSourceDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180);

        RuleFor(x => x.Receiver)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.DesignFlowRate)
            .NotEmpty();
    }
}
