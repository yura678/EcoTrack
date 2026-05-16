using Application.Common.Interfaces.Persistence;
using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Enterprises.Commands;

public class DeleteEnterpriseCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteEnterpriseCommand, Either<EnterpriseException, Enterprise>>
{
    public async Task<Either<EnterpriseException, Enterprise>> Handle(DeleteEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEnterpriseId(request.Id, cancellationToken)
            .BindAsync(e => DeleteEntity(e, cancellationToken));
    }


    private async Task<Either<EnterpriseException, Enterprise>> CheckEnterpriseId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EnterpriseRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<EnterpriseException, Enterprise>>(
            e => e,
            () => new EnterpriseNotFoundException(id)
        );
    }

    private async Task<Either<EnterpriseException, Enterprise>> DeleteEntity(
        Enterprise entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var sites = await unitOfWork.SiteRepository
                .GetByEnterpriseIdAsync(entity.Id, cancellationToken);
            var siteIds = sites.Select(s => s.Id).ToList();

            if (siteIds.Count > 0)
            {
                var installations = await unitOfWork.InstallationRepository
                    .GetBySiteIdsAsync(siteIds, cancellationToken);

                foreach (var installation in installations)
                    installation.Decommission();

                if (installations.Count > 0)
                {
                    var installationIds = installations.Select(i => i.Id).ToList();
                    var devices = await unitOfWork.MonitoringDeviceRepository
                        .GetByInstallationIdsAsync(installationIds, cancellationToken);

                    foreach (var device in devices)
                        device.Decommission();
                }
            }

            foreach (var site in sites)
                unitOfWork.SiteRepository.Delete(site);

            var deletedEnterprise = unitOfWork.EnterpriseRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedEnterprise;
        }
        catch (Exception exception)
        {
            return new UnhandledEnterpriseException(Guid.NewGuid(), exception);
        }
    }
}