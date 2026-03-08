using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators.MonitoringPlans;

public class CreateMonitoringPlanDtoValidator
    : AbstractValidator<CreateMonitoringPlanDto>
{
    public CreateMonitoringPlanDtoValidator()
    {
        RuleFor(x => x.Version)
            .NotEmpty();
        
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        RuleFor(x => x.MonitoringRequirements)
            .NotNull()
            .Must(r => r.Any())
            .WithMessage("Monitoring plan must contain at least one MonitoringRequirement.");


        RuleFor(x => x.MonitoringRequirements)
            .Must(NoDuplicateRequirements)
            .WithMessage("Duplicate MonitoringRequirements are not allowed.");


        RuleForEach(x => x.MonitoringRequirements)
            .SetValidator(new CreateMonitoringRequirementDtoValidator());
    }

    private bool NoDuplicateRequirements(IReadOnlyList<CreateMonitoringRequirementDto> reqs)
    {
        return reqs
            .GroupBy(r => new { r.EmissionSourceId, r.PollutantId })
            .All(g => g.Count() == 1);
    }
}

public class CreateMonitoringRequirementDtoValidator
    : AbstractValidator<CreateMonitoringRequirementDto>
{
    public CreateMonitoringRequirementDtoValidator()
    {
        RuleFor(x => x.EmissionSourceId)
            .NotEmpty();

        RuleFor(x => x.PollutantId)
            .NotEmpty();

        RuleFor(x => x.Frequency)
            .IsInEnum();
    }
}