using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringPlans.Commands;

public class DeleteMonitoringPlanCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteMonitoringPlanCommand, Either<MonitoringPlanException, MonitoringPlan>>
{
    public async Task<Either<MonitoringPlanException, MonitoringPlan>> Handle(
        DeleteMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(m => DeleteEntity(m, cancellationToken));
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
                        "Only Draft plans can be deleted.");
                }

                return m;
            },
            () => new MonitoringPlanNotFoundException(monitoringPlanId)
        );
    }


    private async Task<Either<MonitoringPlanException, MonitoringPlan>> DeleteEntity(
        MonitoringPlan entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedMonitoringPlan = unitOfWork.MonitoringPlanRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedMonitoringPlan;
        }
        catch (Exception exception)
        {
            return new UnhandledMonitoringPlanException(entity.Id, exception);
        }
    }
}