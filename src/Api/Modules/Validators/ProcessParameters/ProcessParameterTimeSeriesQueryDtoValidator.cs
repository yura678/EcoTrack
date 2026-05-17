using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.ProcessParameters;

public class ProcessParameterTimeSeriesQueryDtoValidator
    : AbstractValidator<ProcessParameterTimeSeriesQueryDto>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(31);

    public ProcessParameterTimeSeriesQueryDtoValidator()
    {
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        RuleFor(x => x.ParameterType).IsInEnum();
        RuleFor(x => x.Window).IsInEnum();
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();

        RuleFor(x => x).Must(q => q.From < q.To)
            .WithMessage("'From' must be earlier than 'To'.");
        RuleFor(x => x).Must(q => q.To - q.From <= MaxRange)
            .WithMessage($"Range must not exceed {MaxRange.TotalDays:0} days.");
    }
}
