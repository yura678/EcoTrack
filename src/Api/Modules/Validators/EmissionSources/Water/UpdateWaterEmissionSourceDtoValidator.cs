using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.EmissionSources.Water;

public class UpdateWaterEmissionSourceDtoValidator : AbstractValidator<UpdateWaterEmissionSourceDto>
{
    public UpdateWaterEmissionSourceDtoValidator()
    {
        RuleFor(x => x.Receiver)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.DesignFlowRate)
            .NotEmpty();
    }
}