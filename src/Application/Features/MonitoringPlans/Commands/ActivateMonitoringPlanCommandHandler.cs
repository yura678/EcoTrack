using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringPlans.Commands;

public class ActivateMonitoringPlanCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateMonitoringPlanCommand, Either<MonitoringPlanException, MonitoringPlan>>
{
    public async Task<Either<MonitoringPlanException, MonitoringPlan>> Handle(
        ActivateMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(m => CheckNoActiveMonitoringPlanExists(m, cancellationToken))
            .BindAsync(m => UpdateEntity(m, cancellationToken));
    }

    private async Task<Either<MonitoringPlanException, MonitoringPlan>> CheckId(
        Guid monitoringPlanId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MonitoringPlanRepository.GetByIdAsync(monitoringPlanId, cancellationToken);

        return entity.Match<Either<MonitoringPlanException, MonitoringPlan>>(
            m =>
            {
                if (m.Status != MonitoringPlanStatus.Draft)
                {
                    return new MonitoringPlanInvalidStatusException(
                        monitoringPlanId,
                        m.Status,
                        "Only Draft plans can be activated.");
                }

                return m;
            },
            () => new MonitoringPlanNotFoundException(monitoringPlanId)
        );
    }

    private async Task<Either<MonitoringPlanException, MonitoringPlan>> CheckNoActiveMonitoringPlanExists(
        MonitoringPlan monitoringPlan,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MonitoringPlanRepository.GetActiveByInstallationAsync(
            monitoringPlan.InstallationId, cancellationToken);

        return entity.Match<Either<MonitoringPlanException, MonitoringPlan>>(
            m => new ActiveMonitoringPlanAlreadyExistsException(m.Id),
            () => monitoringPlan
        );
    }

    private async Task<Either<MonitoringPlanException, MonitoringPlan>> UpdateEntity(
        MonitoringPlan entity,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.ChangeStatus(MonitoringPlanStatus.Active);
            var updatedMonitoringPlan = unitOfWork.MonitoringPlanRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updatedMonitoringPlan;
        }
        catch (Exception exception)
        {
            return new UnhandledMonitoringPlanException(entity.Id, exception);
        }
    }
}