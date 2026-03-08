using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Sites.Commands;

public class UpdateSiteCommandCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateSiteCommand, Either<SiteException, Site>>
{
    public async Task<Either<SiteException, Site>> Handle(
        UpdateSiteCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckSite(request.Id, cancellationToken)
            .BindAsync(s => UpdateEntity(s, request, cancellationToken));
    }

    private async Task<Either<SiteException, Site>> CheckSite(
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

    private async Task<Either<SiteException, Site>> UpdateEntity(
        Site site,
        UpdateSiteCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            site.UpdateDetail(request.Name, request.Address, request.SanitaryZoneRadius);
            var updatedSite = unitOfWork.SiteRepository.Update(site);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updatedSite;
        }
        catch (Exception exception)
        {
            return new UnhandledSiteException(Guid.Empty, exception);
        }
    }
}