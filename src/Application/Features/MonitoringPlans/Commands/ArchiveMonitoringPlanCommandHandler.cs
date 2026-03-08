using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringPlans.Commands;



public class ArchiveMonitoringPlanCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<ArchiveMonitoringPlanCommand, Either<MonitoringPlanException, MonitoringPlan>>
{
    public async Task<Either<MonitoringPlanException, MonitoringPlan>> Handle(
        ArchiveMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
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
                if (m.Status != MonitoringPlanStatus.Active)
                {
                    return new MonitoringPlanInvalidStatusException(
                        monitoringPlanId,
                        m.Status,
                        "Only Active monitoring plan can be archive.");
                }

                return m;
            }, () => new MonitoringPlanNotFoundException(monitoringPlanId)
        );
    }


    private async Task<Either<MonitoringPlanException, MonitoringPlan>> UpdateEntity(
        MonitoringPlan entity,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.ChangeStatus(MonitoringPlanStatus.Archived);
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