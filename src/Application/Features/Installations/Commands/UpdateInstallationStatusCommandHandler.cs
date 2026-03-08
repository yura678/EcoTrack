using Application.Common.Interfaces.Persistence;
using Application.Features.Installations.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Installations.Commands;

public class UpdateInstallationStatusCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateInstallationStatusCommand, Either<InstallationException, Installation>>
{
    public async Task<Either<InstallationException, Installation>> Handle(
        UpdateInstallationStatusCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckInstallationId(request.Id, cancellationToken)
            .BindAsync(i => UpdateEntityStatus(i, request, cancellationToken));
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

    private async Task<Either<InstallationException, Installation>> UpdateEntityStatus(
        Installation entity,
        UpdateInstallationStatusCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.ChangeStatus(request.Status);
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