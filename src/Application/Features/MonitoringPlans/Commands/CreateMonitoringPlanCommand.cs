using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.MonitoringPlans.Commands;

public record MonitoringRequirementCommandDto(
    Guid EmissionSourceId,
    Guid PollutantId,
    Frequency Frequency
);

public class CreateMonitoringPlanCommand : IRequest<Either<MonitoringPlanException, MonitoringPlan>>,
    IValidatableModel<CreateMonitoringPlanCommand>
{
    public required Guid InstallationId { get; init; }
    public required string Version { get; init; }
    public required string? Notes { get; init; }
    public required IReadOnlyList<MonitoringRequirementCommandDto> MonitoringRequirements { get; init; }

    public IValidator<CreateMonitoringPlanCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<CreateMonitoringPlanCommand> validator)
    {
        validator.RuleFor(x => x.Version)
            .NotEmpty();

        validator.RuleFor(x => x.InstallationId)
            .NotEmpty();

        validator.RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes is not null);

        validator.RuleFor(x => x.MonitoringRequirements)
            .NotNull()
            .Must(r => r != null && r.Any())
            .WithMessage("Monitoring plan must contain at least one MonitoringRequirement.");

        validator.RuleFor(x => x.MonitoringRequirements)
            .Must(NoDuplicateRequirements)
            .When(x => x.MonitoringRequirements != null)
            .WithMessage("Duplicate MonitoringRequirements are not allowed.");

        validator.RuleForEach(x => x.MonitoringRequirements)
            .SetValidator(new MonitoringRequirementDtoValidator());

        return validator;
    }

    private bool NoDuplicateRequirements(IReadOnlyList<MonitoringRequirementCommandDto> reqs)
    {
        return reqs
            .GroupBy(r => new { r.EmissionSourceId, r.PollutantId })
            .All(g => g.Count() == 1);
    }

    private class MonitoringRequirementDtoValidator : AbstractValidator<MonitoringRequirementCommandDto>
    {
        public MonitoringRequirementDtoValidator()
        {
            RuleFor(x => x.EmissionSourceId)
                .NotEmpty();

            RuleFor(x => x.PollutantId)
                .NotEmpty();

            RuleFor(x => x.Frequency)
                .IsInEnum();
        }
    }
}