using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Repositories.Emissions;
using Application.Common.Interfaces.Repositories.Enterprises;
using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.EmissionSources.Commands.Air;

public class CreateAirEmissionSourceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateAirEmissionSourceCommand, Either<EmissionSourceException, EmissionSource>>
{
    public async Task<Either<EmissionSourceException, EmissionSource>> Handle(
        CreateAirEmissionSourceCommand request,
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
        CreateAirEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newEmissionSource = await unitOfWork.EmissionSourceRepository.AddAsync(
                AirEmissionSource.New(Guid.NewGuid(), request.InstallationId, request.Code, request.Height,
                    request.Diameter, request.DesignFlowRate), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return newEmissionSource;
        }
        catch (Exception exception)
        {
            return new UnhandledEmissionSourceException(Guid.Empty, exception);
        }
    }
}