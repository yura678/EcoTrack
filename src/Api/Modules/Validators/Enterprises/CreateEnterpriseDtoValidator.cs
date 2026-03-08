using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Enterprises;

public class CreateEnterpriseDtoValidator : AbstractValidator<CreateEnterpriseDto>
{
    public CreateEnterpriseDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.RiskGroup)
            .IsInEnum();

        RuleFor(x => x.Edrpou)
            .NotEmpty()
            .MaximumLength(8)
            .Matches(@"^\d{8}$");

        RuleFor(x => x.SectorId)
            .NotEmpty();
    }
}