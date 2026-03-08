using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Installations;

public class UpdateInstallationStatusDtoValidator : AbstractValidator<UpdateInstallationStatusDto>
{
    public UpdateInstallationStatusDtoValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum();
    }
}