using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.MonitoringPlans.Commands;

public record UpdateMonitoringRequirementCommandDto(
    Guid? Id,
    Guid PollutantId,
    Guid EmissionSourceId,
    Frequency Frequency
);
public class UpdateMonitoringPlanCommand : IRequest<Either<MonitoringPlanException, MonitoringPlan>>,
    IValidatableModel<UpdateMonitoringPlanCommand>
{
    public required Guid Id { get; init; }
    public required string Version { get; init; }
    public string? Notes { get; init; }
    public List<UpdateMonitoringRequirementCommandDto> Requirements { get; init; } = [];

    public IValidator<UpdateMonitoringPlanCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<UpdateMonitoringPlanCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        validator.RuleFor(x => x.Version)
            .NotEmpty();
        
        validator.RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        validator.RuleFor(x => x.Requirements)
            .NotNull()
            .Must(r => r != null && r.Count != 0)
            .WithMessage("Monitoring plan must contain at least one MonitoringRequirement.");

        validator.RuleFor(x => x.Requirements)
            .Must(NoDuplicateRequirements)
            .When(x => x.Requirements != null)
            .WithMessage("Duplicate MonitoringRequirements are not allowed.");

        validator.RuleForEach(x => x.Requirements)
            .SetValidator(new UpdateMonitoringRequirementValidator());

        return validator;
    }

    private bool NoDuplicateRequirements(IReadOnlyList<UpdateMonitoringRequirementCommandDto> reqs)
    {
        return reqs
            .GroupBy(r => new { r.EmissionSourceId, r.PollutantId })
            .All(g => g.Count() == 1);
    }

    private class UpdateMonitoringRequirementValidator : AbstractValidator<UpdateMonitoringRequirementCommandDto>
    {
        public UpdateMonitoringRequirementValidator()
        {
            RuleFor(x => x.EmissionSourceId).NotEmpty();
            RuleFor(x => x.PollutantId).NotEmpty();
            RuleFor(x => x.Frequency).IsInEnum();
        }
    }
}