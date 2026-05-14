using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.Measurements;

public class HeatmapQueryDtoValidator : AbstractValidator<HeatmapQueryDto>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(31);

    public HeatmapQueryDtoValidator()
    {
        RuleFor(x => x.PollutantId).NotEmpty();
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();
        RuleFor(x => x.Aggregation).IsInEnum();

        RuleFor(x => x).Must(q => q.From < q.To)
            .WithMessage("'From' must be earlier than 'To'.");

        RuleFor(x => x).Must(q => q.To - q.From <= MaxRange)
            .WithMessage($"Time range must not exceed {MaxRange.TotalDays:0} days.");
    }
}
