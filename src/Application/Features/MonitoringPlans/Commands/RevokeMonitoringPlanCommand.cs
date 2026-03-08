using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Application.Features.MonitoringPlans.Commands;

public class RevokeMonitoringPlanCommand : IRequest<Either<MonitoringPlanException, MonitoringPlan>>,
    IValidatableModel<RevokeMonitoringPlanCommand>
{
    public required Guid Id { get; init; }

    public IValidator<RevokeMonitoringPlanCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<RevokeMonitoringPlanCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}