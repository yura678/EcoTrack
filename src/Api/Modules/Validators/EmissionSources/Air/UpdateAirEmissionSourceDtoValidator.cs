using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.EmissionSources.Air;

public class UpdateAirEmissionSourceDtoValidator : AbstractValidator<UpdateAirEmissionSourceDto>
{
    public UpdateAirEmissionSourceDtoValidator()
    {
        RuleFor(x => x.Height)
            .NotEmpty();

        RuleFor(x => x.Diameter)
            .NotEmpty();

        RuleFor(x => x.DesignFlowRate)
            .NotEmpty();
    }
}