using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringPlans.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.MonitoringPlans.Commands;

public class CreateMonitoringPlanCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateMonitoringPlanCommand, Either<MonitoringPlanException, MonitoringPlan>>
{
    public async Task<Either<MonitoringPlanException, MonitoringPlan>> Handle(
        CreateMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> pollutantIds = request.MonitoringRequirements
            .Select(r => r.PollutantId)
            .Distinct().ToList();

        IReadOnlyList<Guid> emissionSourceIds = request.MonitoringRequirements
            .Select(r => r.EmissionSourceId)
            .Distinct().ToList();

        return await CheckInstallationId(request.InstallationId, cancellationToken)
            .BindAsync(_ => CheckPollutantIds(pollutantIds, cancellationToken))
            .BindAsync(_ => CheckEmissionSourceIds(request.InstallationId, emissionSourceIds, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<MonitoringPlanException, Unit>> CheckInstallationId(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InstallationRepository.GetByIdAsync(installationId, cancellationToken);

        return entity.Match<Either<MonitoringPlanException, Unit>>(
            _ => Unit.Default,
            () => new InstallationNotFoundException(Guid.Empty, installationId)
        );
    }

    private async Task<Either<MonitoringPlanException, Unit>> CheckPollutantIds(
        IReadOnlyList<Guid> pollutantIds,
        CancellationToken cancellationToken)
    {
        var entities = await unitOfWork.PollutantRepository.GetByIdsAsync(pollutantIds, cancellationToken);

        IReadOnlyList<Guid> missingPollutants =
            pollutantIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingPollutants.Any()
            ? new PollutantNotFoundException(Guid.Empty, missingPollutants)
            : Unit.Default;
    }


    private async Task<Either<MonitoringPlanException, Unit>> CheckEmissionSourceIds(
        Guid installationId,
        IReadOnlyList<Guid> emissionSourceIds,
        CancellationToken cancellationToken)
    {
        var entities =
            await unitOfWork.EmissionSourceRepository.GetByInstallationAndIdsAsync(installationId, emissionSourceIds,
                cancellationToken);

        IReadOnlyList<Guid> missingEmissionSources =
            emissionSourceIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingEmissionSources.Any()
            ? new EmissionSourceNotFoundException(Guid.Empty, missingEmissionSources)
            : Unit.Default;
    }


    private async Task<Either<MonitoringPlanException, MonitoringPlan>> CreateEntity(
        CreateMonitoringPlanCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newPlanId = Guid.NewGuid();

            var requirements = request.MonitoringRequirements
                .Select(dto => MonitoringRequirement.New(
                    Guid.NewGuid(),
                    newPlanId,
                    dto.EmissionSourceId,
                    dto.PollutantId,
                    dto.Frequency
                ))
                .ToArray();

            var newMonitoringPlan = await unitOfWork.MonitoringPlanRepository.AddAsync(
                MonitoringPlan.New(newPlanId, request.InstallationId, request.Version,
                    request.Notes, requirements), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return newMonitoringPlan;
        }
        catch (Exception exception)
        {
            return new UnhandledMonitoringPlanException(Guid.Empty, exception);
        }
    }
}