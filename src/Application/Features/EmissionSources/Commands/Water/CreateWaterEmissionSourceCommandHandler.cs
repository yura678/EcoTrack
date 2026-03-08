using Application.Common.Interfaces.Persistence;
using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.EmissionSources.Commands.Water;

public class CreateWaterEmissionSourceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateWaterEmissionSourceCommand, Either<EmissionSourceException, EmissionSource>>
{
    public async Task<Either<EmissionSourceException, EmissionSource>> Handle(CreateWaterEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckInstallationId(request.InstallationId, cancellationToken)
            .BindAsync(_ => CheckCode(request.InstallationId, request.Code, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<EmissionSourceException, Unit>> CheckInstallationId(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InstallationRepository.GetByIdAsync(installationId, cancellationToken);

        return entity.Match<Either<EmissionSourceException, Unit>>(
            _ => Unit.Default,
            () => new InstallationNotFoundException(Guid.Empty, installationId)
        );
    }

    private async Task<Either<EmissionSourceException, Unit>> CheckCode(
        Guid installationId,
        string code,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EmissionSourceRepository.GetByCodeAsync(installationId, code, cancellationToken);

        return entity.Match<Either<EmissionSourceException, Unit>>(
            e => new EmissionSourceCodeAlreadyExistsException(e.Id, code),
            () => Unit.Default
        );
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> CreateEntity(
        CreateWaterEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newEmission = await unitOfWork.EmissionSourceRepository.AddAsync(
                WaterEmissionSource.New(Guid.NewGuid(), request.InstallationId, request.Code, request.Receiver,
                    request.DesignFlowRate), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return newEmission;
        }
        catch (Exception exception)
        {
            return new UnhandledEmissionSourceException(Guid.Empty, exception);
        }
    }
}