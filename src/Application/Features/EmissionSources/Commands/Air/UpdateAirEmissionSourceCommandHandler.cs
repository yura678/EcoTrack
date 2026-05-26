using Application.Common.Interfaces.Persistence;
using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.EmissionSources.Commands.Air;

public class UpdateAirEmissionSourceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateAirEmissionSourceCommand, Either<EmissionSourceException, EmissionSource>>
{
    public async Task<Either<EmissionSourceException, EmissionSource>> Handle(UpdateAirEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEmissionSourceId(request.Id, cancellationToken)
            .BindAsync(CheckType)
            .BindAsync(e => CheckCode(e, request.Code, cancellationToken))
            .BindAsync(e => UpdateEntity(e, request, cancellationToken));
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> CheckEmissionSourceId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EmissionSourceRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<EmissionSourceException, EmissionSource>>(
            e => e,
            () => new EmissionSourceNotFoundException(id)
        );
    }

    private Either<EmissionSourceException, AirEmissionSource> CheckType(
        EmissionSource emissionSource)
    {
        if (emissionSource is AirEmissionSource airEmission)
        {
            return airEmission;
        }

        return new EmissionSourceTypeMismatchException(emissionSource.Id, typeof(AirEmissionSource),
            emissionSource.GetType());
    }

    private async Task<Either<EmissionSourceException, AirEmissionSource>> CheckCode(
        AirEmissionSource entity,
        string newCode,
        CancellationToken cancellationToken)
    {
        if (entity.Code == newCode)
        {
            return entity;
        }

        var existing = await unitOfWork.EmissionSourceRepository
            .GetByCodeIncludingDeletedAsync(entity.InstallationId, newCode, cancellationToken);

        return existing.Match<Either<EmissionSourceException, AirEmissionSource>>(
            e => new EmissionSourceCodeAlreadyExistsException(e.Id, newCode),
            () => entity
        );
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> UpdateEntity(
        AirEmissionSource entity,
        UpdateAirEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (entity.Code != request.Code)
            {
                entity.UpdateCode(request.Code);
            }

            entity.UpdateLocation(request.Latitude, request.Longitude);
            entity.UpdateDetails(request.Height, request.Diameter, request.DesignFlowRate);

            var updatedEmission = unitOfWork.EmissionSourceRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updatedEmission;
        }
        catch (Exception exception)
        {
            return new UnhandledEmissionSourceException(request.Id, exception);
        }
    }
}
