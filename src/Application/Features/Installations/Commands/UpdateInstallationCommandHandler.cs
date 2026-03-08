using Application.Common.Interfaces.Persistence;
using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Installations.Commands;

public class UpdateInstallationCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateInstallationCommand, Either<InstallationException, Installation>>
{
    public async Task<Either<InstallationException, Installation>> Handle(
        UpdateInstallationCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckInstallationId(request.Id, cancellationToken)
            .BindAsync(i => CheckIedCategoryId(i, request.IedCategoryId, cancellationToken))
            .BindAsync(i => UpdateEntity(i, request, cancellationToken));
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

    private async Task<Either<InstallationException, Installation>> CheckIedCategoryId(
        Installation installation,
        Guid iedCategoryId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.IedCategoryRepository.GetByIdAsync(iedCategoryId, cancellationToken);

        return entity.Match<Either<InstallationException, Installation>>(
            _ => installation,
            () => new IedCategoryNotFoundException(installation.Id, iedCategoryId)
        );
    }

    private async Task<Either<InstallationException, Installation>> UpdateEntity(
        Installation entity,
        UpdateInstallationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(request.Name, request.IedCategoryId);
            var updatedInstallation = unitOfWork.InstallationRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updatedInstallation;
        }
        catch (Exception exception)
        {
            return new UnhandledInstallationException(request.Id, exception);
        }
    }
}