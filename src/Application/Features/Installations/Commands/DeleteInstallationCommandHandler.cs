using Application.Common.Interfaces.Persistence;
using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Installations.Commands;

public class DeleteInstallationCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteInstallationCommand, Either<InstallationException, Installation>>
{
    public async Task<Either<InstallationException, Installation>> Handle(
        DeleteInstallationCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckInstallationId(request.Id, cancellationToken)
            .BindAsync(i => CheckDependencies(i, cancellationToken))
            .BindAsync(i => UpdateEntity(i, cancellationToken));
    }

    private async Task<Either<InstallationException, Installation>> CheckInstallationId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InstallationRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<InstallationException, Installation>>(
            i => i,
            () => new InstallationNotFoundException(id)
        );
    }

    private async Task<Either<InstallationException, Installation>> CheckDependencies(
        Installation entity,
        CancellationToken cancellationToken)
    {
        var hasDependencies =
            await unitOfWork.InstallationRepository.HasDependenciesAsync(entity.Id, cancellationToken);

        return hasDependencies
            ? new InstallationHasDependenciesException(entity.Id)
            : entity;
    }

    private async Task<Either<InstallationException, Installation>> UpdateEntity(
        Installation entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedInstallation = unitOfWork.InstallationRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedInstallation;
        }

        catch (Exception exception)
        {
            return new UnhandledInstallationException(entity.Id, exception);
        }
    }
}