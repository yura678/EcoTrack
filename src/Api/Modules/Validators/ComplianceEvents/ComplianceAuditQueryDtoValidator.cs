using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.ComplianceEvents;

public class ComplianceAuditQueryDtoValidator : AbstractValidator<ComplianceAuditQueryDto>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(366 * 5); // 5 years

    public ComplianceAuditQueryDtoValidator()
    {
        RuleFor(x => x.EmissionSourceId).NotEmpty();
        RuleFor(x => x.PollutantId).NotEmpty();
        RuleFor(x => x.LimitUnitId).NotEmpty();
        RuleFor(x => x.LimitValue).GreaterThan(0);
        RuleFor(x => x.Period).IsInEnum();
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty();

        RuleFor(x => x).Must(q => q.From < q.To)
            .WithMessage("'From' must be earlier than 'To'.");

        RuleFor(x => x).Must(q => q.To - q.From <= MaxRange)
            .WithMessage($"Range must not exceed {MaxRange.TotalDays:0} days.");
    }
}
