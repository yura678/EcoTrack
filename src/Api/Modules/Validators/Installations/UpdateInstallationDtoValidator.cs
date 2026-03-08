using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Installations;

public class UpdateInstallationDtoValidator : AbstractValidator<UpdateInstallationDto>
{
    public UpdateInstallationDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.IedCategoryId)
            .NotEmpty();
    }
}