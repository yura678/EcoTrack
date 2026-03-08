using Application.Common.Interfaces.Persistence;
using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.EmissionSources.Commands;

public class DeleteEmissionSourceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteEmissionSourceCommand, Either<EmissionSourceException, EmissionSource>>
{
    public async Task<Either<EmissionSourceException, EmissionSource>> Handle(DeleteEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEmissionSourceId(request, cancellationToken)
            .BindAsync(e => CheckDependencies(e, cancellationToken))
            .BindAsync(e => DeleteEntity(e, cancellationToken));
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> CheckEmissionSourceId(
        DeleteEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EmissionSourceRepository.GetByIdAsync(request.Id, cancellationToken);

        return entity.Match<Either<EmissionSourceException, EmissionSource>>(
            e => e,
            () => new EmissionSourceNotFoundException(request.Id)
        );
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> CheckDependencies(
        EmissionSource emissionSource,
        CancellationToken cancellationToken)
    {
        var hasDependencies =
            await unitOfWork.EmissionSourceRepository.HasDependenciesAsync(emissionSource.Id, cancellationToken);

        return hasDependencies
            ? new EmissionSourceHasDependenciesException(emissionSource.Id)
            : emissionSource;
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> DeleteEntity(
        EmissionSource entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedEmission = unitOfWork.EmissionSourceRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedEmission;
        }
        catch (Exception exception)
        {
            return new UnhandledEmissionSourceException(entity.Id, exception);
        }
    }
}