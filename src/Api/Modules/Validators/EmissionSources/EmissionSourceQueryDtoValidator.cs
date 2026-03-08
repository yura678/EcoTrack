using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.EmissionSources;

public class EmissionSourceQueryDtoValidator : AbstractValidator<EmissionSourceQueryDto>
{
    public EmissionSourceQueryDtoValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum();
        
        RuleFor(x => x.Page)
            .NotEmpty().GreaterThanOrEqualTo(1);
        
        RuleFor(x => x.PageSize)
            .NotEmpty().GreaterThan(0);

    }

    
}