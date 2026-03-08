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
            .BindAsync(e => CheckDependencies(e, cancellationToken))
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

    private async Task<Either<EnterpriseException, Enterprise>> CheckDependencies(
        Enterprise entity,
        CancellationToken cancellationToken)
    {
        bool hasDependencies = await unitOfWork.EnterpriseRepository.HasDependenciesAsync(entity.Id, cancellationToken);

        return hasDependencies
            ? new EnterpriseHasDependenciesException(entity.Id)
            : entity;
    }

    private async Task<Either<EnterpriseException, Enterprise>> DeleteEntity(
        Enterprise entity,
        CancellationToken cancellationToken)
    {
        try
        {
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