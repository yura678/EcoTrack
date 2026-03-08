using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using FluentValidation;
using LanguageExt;
using MediatR;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;


namespace Application.Features.MonitoringPlans.Commands;

public class DeleteMonitoringPlanCommand : IRequest<Either<MonitoringPlanException, MonitoringPlan>>,
    IValidatableModel<DeleteMonitoringPlanCommand>
{
    public required Guid Id { get; init; }

    public IValidator<DeleteMonitoringPlanCommand> ValidateApplicationModel(
        ApplicationBaseValidationModelProvider<DeleteMonitoringPlanCommand> validator)
    {
        validator.RuleFor(x => x.Id)
            .NotEmpty();

        return validator;
    }
}