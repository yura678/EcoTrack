using Application.Common.Interfaces.Persistence;
using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Enterprises.Commands;

public class RestoreEnterpriseCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<RestoreEnterpriseCommand, Either<EnterpriseException, Enterprise>>
{
    public async Task<Either<EnterpriseException, Enterprise>> Handle(RestoreEnterpriseCommand request,
        CancellationToken cancellationToken)
    {
        var entityOption = await unitOfWork.EnterpriseRepository
            .GetByIdIncludingDeletedAsync(request.Id, cancellationToken);

        var entity = entityOption.Match(e => e, () => null!);
        if (entity is null || entity.DeletedAt is null)
            return new EnterpriseNotFoundException(request.Id);

        entity.Restore();
        unitOfWork.EnterpriseRepository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
