using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Installations;

public class CreateInstallationDtoValidator : AbstractValidator<CreateInstallationDto>
{
    public CreateInstallationDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.IedCategoryId)
            .NotEmpty();

        RuleFor(x => x.SiteId)
            .NotEmpty();

        RuleFor(x => x.Status)
            .IsInEnum();
    }
}