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
            .BindAsync(s => CheckDependencies(s, cancellationToken))
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

    private async Task<Either<SiteException, Site>> CheckDependencies(
        Site entity,
        CancellationToken cancellationToken)
    {
        var hasDependencies = await unitOfWork.SiteRepository.HasDependenciesAsync(entity.Id, cancellationToken);

        return hasDependencies
            ? new SiteHasDependenciesException(entity.Id)
            : entity;
    }

    private async Task<Either<SiteException, Site>> DeleteEntity(
        Site site,
        CancellationToken cancellationToken)
    {
        try
        {
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