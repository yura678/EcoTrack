using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.EmissionSources.Air;

public class CreateAirEmissionSourceDtoValidator : AbstractValidator<CreateAirEmissionSourceDto>
{
    public CreateAirEmissionSourceDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Height)
            .NotEmpty();

        RuleFor(x => x.Diameter)
            .NotEmpty();

        RuleFor(x => x.DesignFlowRate)
            .NotEmpty();

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180);
    }
}
