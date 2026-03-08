using Application.Common.Interfaces.Persistence;
using Application.Features.Pollutants.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.Pollutants.Commands;

public class UpdatePollutantCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePollutantCommand, Either<PollutantException, Pollutant>>
{
    public async Task<Either<PollutantException, Pollutant>> Handle(
        UpdatePollutantCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckPollutantId(request.Id, cancellationToken)
            .BindAsync(p => CheckPollutantName(p, request.Name, cancellationToken))
            .BindAsync(p => CheckPollutantCode(p, request.Code, cancellationToken))
            .BindAsync(p => UpdateEntity(p, request, cancellationToken));
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

    private async Task<Either<PollutantException, Pollutant>> CheckPollutantCode(
        Pollutant pollutant,
        string code,
        CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.PollutantRepository.GetByCodeAsync(code, cancellationToken);

        return existing.Match<Either<PollutantException, Pollutant>>(
            p => p.Id == pollutant.Id
                ? pollutant // свій же код - норм
                : new PollutantCodeAlreadyExistsException(p.Id, p.Code),
            () => pollutant // немає конфлікту
        );
    }

    private async Task<Either<PollutantException, Pollutant>> CheckPollutantName(
        Pollutant pollutant,
        string name,
        CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.PollutantRepository.GetByNameAsync(name, cancellationToken);

        return existing.Match<Either<PollutantException, Pollutant>>(
            p => p.Id == pollutant.Id
                ? pollutant // не конфліктує сам із собою
                : new PollutantNameAlreadyExistsException(p.Id, p.Name),
            () => pollutant
        );
    }

    private async Task<Either<PollutantException, Pollutant>> UpdateEntity(
        Pollutant entity,
        UpdatePollutantCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(request.Name, request.Code);
            var updatedPollutant = unitOfWork.PollutantRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return updatedPollutant;
        }
        catch (Exception exception)
        {
            return new UnhandledPollutantException(Guid.Empty, exception);
        }
    }
}