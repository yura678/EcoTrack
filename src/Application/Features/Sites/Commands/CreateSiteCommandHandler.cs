using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Sites.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Sites.Commands;

public class CreateSiteCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService curentUserService)
    : IRequestHandler<CreateSiteCommand, Either<SiteException, Site>>
{
    public async Task<Either<SiteException, Site>> Handle(
        CreateSiteCommand request,
        CancellationToken cancellationToken)
    {
        
        Guid? currentEnterpriseId = curentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = curentUserService.IsSuperAdmin();

        if (!isSuperAdmin)
        {
            if (!currentEnterpriseId.HasValue || currentEnterpriseId.Value != request.EnterpriseId)
            {
                return new EnterpriseNotFoundException(Guid.Empty, request.EnterpriseId);
            }
        }
        
        return await CheckEnterprise(request.EnterpriseId, cancellationToken)
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<SiteException, Unit>> CheckEnterprise(
        Guid enterpriseId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EnterpriseRepository.GetByIdAsync(enterpriseId, cancellationToken);

        return entity.Match<Either<SiteException, Unit>>(
            e => Unit.Default,
            () => new EnterpriseNotFoundException(Guid.Empty, enterpriseId)
        );
    }

    private async Task<Either<SiteException, Site>> CreateEntity(
        CreateSiteCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newSite = await unitOfWork.SiteRepository.AddAsync(
                Site.New(Guid.NewGuid(), request.Name, request.Address, request.SanitaryZoneRadius,
                    request.EnterpriseId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return newSite;
        }
        catch (Exception exception)
        {
            return new UnhandledSiteException(Guid.NewGuid(), exception);
        }
    }
}