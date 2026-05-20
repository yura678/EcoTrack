using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Sites.Commands;

public class RestoreSiteCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<RestoreSiteCommand, Either<SiteException, Site>>
{
    public async Task<Either<SiteException, Site>> Handle(RestoreSiteCommand request,
        CancellationToken cancellationToken)
    {
        var siteOption = await unitOfWork.SiteRepository
            .GetByIdIncludingDeletedAsync(request.Id, cancellationToken);

        return await siteOption.MatchAsync<Site, Either<SiteException, Site>>(
            Some: async site =>
            {
                if (site.DeletedAt is null)
                    return new SiteNotFoundException(request.Id);

                if (!currentUserService.IsSuperAdmin())
                {
                    var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
                    if (currentEnterpriseId is null || site.EnterpriseId != currentEnterpriseId)
                        return new SiteNotFoundException(request.Id);
                }

                site.Restore();
                unitOfWork.SiteRepository.Update(site);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return site;
            },
            None: () => new SiteNotFoundException(request.Id));
    }
}
