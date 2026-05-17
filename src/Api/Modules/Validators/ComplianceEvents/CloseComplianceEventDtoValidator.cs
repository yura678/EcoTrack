using Api.Dtos;
using Domain.Entities.Monitoring;
using FluentValidation;

namespace Api.Modules.Validators.ComplianceEvents;

public class CloseComplianceEventDtoValidator : AbstractValidator<CloseComplianceEventDto>
{
    public CloseComplianceEventDtoValidator()
    {
        RuleFor(x => x.Reason).IsInEnum();

        // Free-form reason → audit trail needs an explanation, otherwise the closure is empty.
        RuleFor(x => x.Note)
            .NotEmpty()
            .When(x => x.Reason == ResolutionReason.Other)
            .WithMessage("Note is required when Reason is Other.");

        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
