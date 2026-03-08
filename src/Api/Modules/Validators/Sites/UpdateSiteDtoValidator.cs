using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Sites;

public class UpdateSiteDtoValidator : AbstractValidator<UpdateSiteDto>
{
    public UpdateSiteDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);
        

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.SanitaryZoneRadius)
            .GreaterThan(0);
        
    }
}