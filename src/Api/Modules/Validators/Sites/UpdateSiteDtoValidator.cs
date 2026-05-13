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

        When(x => x.Latitude.HasValue, () =>
        {
            RuleFor(x => x.Latitude!.Value)
                .InclusiveBetween(-90, 90);
        });

        When(x => x.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Longitude!.Value)
                .InclusiveBetween(-180, 180);
        });
    }
}