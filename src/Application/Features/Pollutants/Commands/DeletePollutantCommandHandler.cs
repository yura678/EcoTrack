using Application.Common.Interfaces.Persistence;
using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.Pollutants.Commands;

public class DeletePollutantCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePollutantCommand, Either<PollutantException, Pollutant>>
{
    public async Task<Either<PollutantException, Pollutant>> Handle(
        DeletePollutantCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckPollutantId(request.Id, cancellationToken)
            .BindAsync(p => CheckDependencies(p, cancellationToken))
            .BindAsync(p => DeleteEntity(p, cancellationToken));
    }

    private async Task<Either<PollutantException, Pollutant>> CheckPollutantId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PollutantRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<PollutantException, Pollutant>>(
            p => p,
            () => new PollutantNotFoundException(id)
        );
    }

    private async Task<Either<PollutantException, Pollutant>> CheckDependencies(
        Pollutant entity,
        CancellationToken cancellationToken)
    {
        bool hasDeps = await unitOfWork.PollutantRepository.HasDependenciesAsync(entity.Id, cancellationToken);

        return hasDeps
            ? new PollutantHasDependenciesException(entity.Id)
            : entity;
    }

    private async Task<Either<PollutantException, Pollutant>> DeleteEntity(
        Pollutant entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedPollutant = unitOfWork.PollutantRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedPollutant;
        }
        catch (Exception exception)
        {
            return new UnhandledPollutantException(Guid.Empty, exception);
        }
    }
}