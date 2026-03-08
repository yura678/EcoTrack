using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Enterprises;

public class UpdateEnterpriseDtoValidator : AbstractValidator<UpdateEnterpriseDto>
{
    public UpdateEnterpriseDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.RiskGroup)
            .IsInEnum();


        RuleFor(x => x.SectorId)
            .NotEmpty();
    }
}