using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Sites.Commands;

public class DeleteSiteCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteSiteCommand, Either<SiteException, Site>>
{
    public async Task<Either<SiteException, Site>> Handle(
        DeleteSiteCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckSiteId(request.Id, cancellationToken)
            .BindAsync(s => DeleteEntity(s, cancellationToken));
    }

    private async Task<Either<SiteException, Site>> CheckSiteId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.SiteRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<SiteException, Site>>(
            s =>
            {
                var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
                bool isSuperAdmin = currentUserService.IsSuperAdmin();

                if (isSuperAdmin)
                {
                    return s;
                }

                if (!currentEnterpriseId.HasValue || s.EnterpriseId != currentEnterpriseId.Value)
                {
                    return new SiteNotFoundException(id);
                }

                return s;
            },
            () => new SiteNotFoundException(id)
        );
    }

    private async Task<Either<SiteException, Site>> DeleteEntity(
        Site site,
        CancellationToken cancellationToken)
    {
        try
        {
            // Invariant: archiving a Site is an end-of-life action, not a "decommission
            // everything for me" shortcut. Installations have their own status lifecycle and
            // their own cascade rules (devices, permits). Forcing the operator to flip those
            // explicitly avoids silent data loss — once a device's parent Site has DeletedAt,
            // the global query filter hides the device, HMAC ingest starts returning 401, and
            // the device's data goes nowhere with no clear log signal.
            //
            // CountOperatingBySiteAsync is no-tracking on purpose: if we loaded installations
            // tracked, EF would see "Site marked Deleted with tracked children that have a
            // required FK to it" and throw on save. We just need the count.
            var operatingCount = await unitOfWork.InstallationRepository
                .CountOperatingBySiteAsync(site.Id, cancellationToken);
            if (operatingCount > 0)
                return new SiteHasOperatingInstallationsException(site.Id, operatingCount);

            var deletedSite = unitOfWork.SiteRepository.Delete(site);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedSite;
        }
        catch (Exception exception)
        {
            return new UnhandledSiteException(Guid.Empty, exception);
        }
    }
}