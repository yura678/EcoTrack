using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.MonitoringPlans.Commands;

public class ActivateMonitoringPlanCommand : IRequest<Either<MonitoringPlanException, MonitoringPlan>>,
    IValidatableModel<ActivateMonitoringPlanCommand>
{
    public required Guid Id { get; init; }

    public IValidator<ActivateMonitoringPlanCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<ActivateMonitoringPlanCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}