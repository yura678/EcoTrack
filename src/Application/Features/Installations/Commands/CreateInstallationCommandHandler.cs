using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Installations.Commands;



public class CreateInstallationCommandHandler(
   IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    : IRequestHandler<CreateInstallationCommand, Either<InstallationException, Installation>>
{
    public async Task<Either<InstallationException, Installation>> Handle(
        CreateInstallationCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckSiteId(request.SiteId, cancellationToken)
            .BindAsync(_ => CheckIedCategoryId(request.IedCategoryId, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }
    
    

    private async Task<Either<InstallationException, Unit>> CheckSiteId(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.SiteRepository.GetByIdAsync(siteId, cancellationToken);

        return entity.Match<Either<InstallationException, Unit>>(
            site => 
            {
                return Unit.Default;
            },
            () => new SiteNotFoundException(Guid.Empty, siteId)
        );
    }

    private async Task<Either<InstallationException, Unit>> CheckIedCategoryId(
        Guid iedCategoryId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.IedCategoryRepository.GetByIdAsync(iedCategoryId, cancellationToken);

        return entity.Match<Either<InstallationException, Unit>>(
            _ => Unit.Default,
            () => new IedCategoryNotFoundException(Guid.Empty, iedCategoryId)
        );
    }

    private async Task<Either<InstallationException, Installation>> CreateEntity(
        CreateInstallationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newInstallation = await unitOfWork.InstallationRepository.AddAsync(
                Installation.New(Guid.NewGuid(), request.Name, request.IedCategoryId, request.SiteId,
                    request.Status), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return newInstallation;
        }
        catch (Exception exception)
        {
            return new UnhandledInstallationException(Guid.Empty, exception);
        }
    }
}