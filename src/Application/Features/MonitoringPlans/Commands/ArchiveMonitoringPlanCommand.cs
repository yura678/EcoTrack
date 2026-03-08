using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.MonitoringPlans.Commands;

public class ArchiveMonitoringPlanCommand : IRequest<Either<MonitoringPlanException, MonitoringPlan>>,
    IValidatableModel<ArchiveMonitoringPlanCommand>
{
    public required Guid Id { get; init; }

    public IValidator<ArchiveMonitoringPlanCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ArchiveMonitoringPlanCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}