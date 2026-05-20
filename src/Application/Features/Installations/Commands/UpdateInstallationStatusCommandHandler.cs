using Application.Common.Interfaces.Persistence;
using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Installations.Commands;

public class UpdateInstallationStatusCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateInstallationStatusCommand, Either<InstallationException, Installation>>
{
    public async Task<Either<InstallationException, Installation>> Handle(
        UpdateInstallationStatusCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckInstallationId(request.Id, cancellationToken)
            .BindAsync(i => UpdateEntityStatus(i, request, cancellationToken));
    }

    private async Task<Either<InstallationException, Installation>> CheckInstallationId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InstallationRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<InstallationException, Installation>>(
            i => i,
            () => new InstallationNotFoundException(id)
        );
    }

    private async Task<Either<InstallationException, Installation>> UpdateEntityStatus(
        Installation entity,
        UpdateInstallationStatusCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var isDecommissioningTransition =
                entity.Status != InstallationStatus.Decommissioned
                && request.Status == InstallationStatus.Decommissioned;

            entity.ChangeStatus(request.Status);
            unitOfWork.InstallationRepository.Update(entity);

            // Cascade only on the Operating/Shutdown → Decommissioned transition. Reverse moves
            // (Decommissioned → Operating, e.g. re-commissioning a site) intentionally do NOT
            // restore devices or permits — bringing infrastructure back into service is an
            // explicit per-asset decision the operator must make.
            if (isDecommissioningTransition)
            {
                var devices = await unitOfWork.MonitoringDeviceRepository
                    .GetNonDecommissionedByInstallationAsync(entity.Id, cancellationToken);
                foreach (var device in devices) device.Decommission();

                var activePermits = await unitOfWork.PermitRepository
                    .GetActiveByInstallationAsync(entity.Id, cancellationToken);
                foreach (var permit in activePermits) permit.Revoke();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch (Exception exception)
        {
            return new UnhandledInstallationException(request.Id, exception);
        }
    }
}