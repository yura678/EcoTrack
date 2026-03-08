using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringPlans.Commands;

public class UpdateMonitoringPlanCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateMonitoringPlanCommand, Either<MonitoringPlanException, MonitoringPlan>>
{
    public async Task<Either<MonitoringPlanException, MonitoringPlan>> Handle(
        UpdateMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await HandleAsync(request, cancellationToken);
            if (result.IsLeft)
            {
                transaction.Rollback();
            }
            else
            {
                transaction.Commit();
            }

            return result;
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return new UnhandledMonitoringPlanException(request.Id, exception);
        }
    }

    private async Task<Either<MonitoringPlanException, MonitoringPlan>> HandleAsync(
        UpdateMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> pollutantIds = request.Requirements
            .Select(r => r.PollutantId)
            .Distinct().ToList();

        IReadOnlyList<Guid> emissionSourceIds = request.Requirements
            .Select(r => r.EmissionSourceId)
            .Distinct().ToList();

        return await CheckMonitoringPlanId(request.Id, cancellationToken)
            .BindAsync(m => CheckPollutantIds(m, pollutantIds, cancellationToken))
            .BindAsync(m => CheckEmissionSourceIds(m, emissionSourceIds, cancellationToken))
            .BindAsync(m => UpdateEntity(m, request, cancellationToken));
    }

    private async Task<Either<MonitoringPlanException, MonitoringPlan>> CheckMonitoringPlanId(
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
                        "Only Draft plans can be modified. Please create a new version.");
                }

                return m;
            },
            () => new MonitoringPlanNotFoundException(monitoringPlanId)
        );
    }

    private async Task<Either<MonitoringPlanException, MonitoringPlan>> CheckPollutantIds(
        MonitoringPlan entity,
        IReadOnlyList<Guid> pollutantIds,
        CancellationToken cancellationToken)
    {
        var entities = await unitOfWork.PollutantRepository.GetByIdsAsync(pollutantIds, cancellationToken);

        IReadOnlyList<Guid> missingPollutants =
            pollutantIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingPollutants.Any()
            ? new PollutantNotFoundException(Guid.Empty, missingPollutants)
            : entity;
    }


    private async Task<Either<MonitoringPlanException, MonitoringPlan>> CheckEmissionSourceIds(
        MonitoringPlan entity,
        IReadOnlyList<Guid> emissionSourceIds,
        CancellationToken cancellationToken)
    {
        var entities =
            await unitOfWork.EmissionSourceRepository.GetByInstallationAndIdsAsync(entity.InstallationId,
                emissionSourceIds,
                cancellationToken);

        IReadOnlyList<Guid> missingEmissionSources =
            emissionSourceIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingEmissionSources.Any()
            ? new EmissionSourceNotFoundException(Guid.Empty, missingEmissionSources)
            : entity;
    }


    private async Task<Either<MonitoringPlanException, MonitoringPlan>> UpdateEntity(
        MonitoringPlan plan,
        UpdateMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        var dtoRequirements = request.Requirements;

        var existingMap = plan.Requirements
            .ToDictionary(r => r.Id, r => r);

        var toRemove = new List<MonitoringRequirement>();
        var toAdd = new List<MonitoringRequirement>();
        var toUpdate = new List<MonitoringRequirement>();

        foreach (var dto in dtoRequirements)
        {
            if (dto.Id is null)
            {
                toAdd.Add(MonitoringRequirement.New(
                    Guid.NewGuid(),
                    plan.Id,
                    dto.EmissionSourceId,
                    dto.PollutantId,
                    dto.Frequency
                ));
                continue;
            }

            if (!existingMap.TryGetValue(dto.Id.Value, out var entity))
            {
                return new MonitoringRequirementNotFoundException(plan.Id, dto.Id.Value);
            }

            entity.UpdateDetails(
                dto.Frequency,
                dto.PollutantId,
                dto.EmissionSourceId
            );

            toUpdate.Add(entity);
            existingMap.Remove(dto.Id.Value); // залишок → те, що потрібно видалити
        }

        toRemove.AddRange(existingMap.Values);

        if (toRemove.Any())
            unitOfWork.MonitoringRequirementRepository.RemoveRange(toRemove);

        if (toUpdate.Any())
            unitOfWork.MonitoringRequirementRepository.UpdateRange(toUpdate);

        if (toAdd.Any())
            await unitOfWork.MonitoringRequirementRepository.AddRangeAsync(toAdd, cancellationToken);

        plan.UpdateDetails(request.Notes, request.Version);

        var updated = unitOfWork.MonitoringPlanRepository.Update(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return updated;
    }
}