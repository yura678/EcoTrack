using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.EmissionSources.Commands;

public class RestoreEmissionSourceCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService)
    : IRequestHandler<RestoreEmissionSourceCommand, Either<EmissionSourceException, EmissionSource>>
{
    public async Task<Either<EmissionSourceException, EmissionSource>> Handle(RestoreEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        var entityOption = await unitOfWork.EmissionSourceRepository
            .GetByIdIncludingDeletedAsync(request.Id, cancellationToken);

        var entity = entityOption.Match(e => e, () => null!);
        if (entity is null || entity.DeletedAt is null)
            return new EmissionSourceNotFoundException(request.Id);

        if (!currentUserService.IsSuperAdmin())
        {
            var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
            if (currentEnterpriseId is null || entity.EnterpriseId != currentEnterpriseId)
                return new EmissionSourceNotFoundException(request.Id);
        }

        entity.Restore();
        unitOfWork.EmissionSourceRepository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
