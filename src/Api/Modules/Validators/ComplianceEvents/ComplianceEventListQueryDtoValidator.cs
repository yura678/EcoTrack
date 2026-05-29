using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.ComplianceEvents;

public class ComplianceEventListQueryDtoValidator : AbstractValidator<ComplianceEventListQueryDto>
{
    public ComplianceEventListQueryDtoValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);

        When(x => x.Status.HasValue, () => RuleFor(x => x.Status!.Value).IsInEnum());
        When(x => x.EventType.HasValue, () => RuleFor(x => x.EventType!.Value).IsInEnum());
        When(x => x.ResolutionReason.HasValue, () => RuleFor(x => x.ResolutionReason!.Value).IsInEnum());

        When(x => x.From.HasValue && x.To.HasValue,
            () => RuleFor(x => x).Must(q => q.From!.Value <= q.To!.Value)
                .WithMessage("'From' must be earlier than or equal to 'To'."));
    }
}
