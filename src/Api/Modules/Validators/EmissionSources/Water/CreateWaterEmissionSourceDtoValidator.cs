using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.EmissionSources.Water;

public class CreateWaterEmissionSourceDtoValidator : AbstractValidator<CreateWaterEmissionSourceDto>
{
    public CreateWaterEmissionSourceDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50);


        RuleFor(x => x.Receiver)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.DesignFlowRate)
            .NotEmpty();
    }
}