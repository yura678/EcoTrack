using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Measurements;

public class MeasurementQueryDtoValidator : AbstractValidator<MeasurementQueryDto>
{
    public MeasurementQueryDtoValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);

        When(x => x.Window.HasValue, () => RuleFor(x => x.Window!.Value).IsInEnum());
        When(x => x.Quality.HasValue, () => RuleFor(x => x.Quality!.Value).IsInEnum());
        RuleFor(x => x.Sort).IsInEnum();
        RuleFor(x => x.Direction).IsInEnum();

        When(x => x.From.HasValue && x.To.HasValue,
            () => RuleFor(x => x).Must(q => q.From!.Value <= q.To!.Value)
                .WithMessage("'From' must be earlier than or equal to 'To'."));
    }
}

public class MeasurementSummaryQueryDtoValidator : AbstractValidator<MeasurementSummaryQueryDto>
{
    public MeasurementSummaryQueryDtoValidator()
    {
        When(x => x.Window.HasValue, () => RuleFor(x => x.Window!.Value).IsInEnum());
        When(x => x.Quality.HasValue, () => RuleFor(x => x.Quality!.Value).IsInEnum());

        When(x => x.From.HasValue && x.To.HasValue,
            () => RuleFor(x => x).Must(q => q.From!.Value <= q.To!.Value)
                .WithMessage("'From' must be earlier than or equal to 'To'."));
    }
}
