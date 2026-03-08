using Application.Common.Interfaces.Persistence;
using Application.Features.EmissionSources.Exceptions;
using Domain.Entities.EmissionSources;
using LanguageExt;
using MediatR;

namespace Application.Features.EmissionSources.Commands.Water;



public class UpdateWaterEmissionSourceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateWaterEmissionSourceCommand, Either<EmissionSourceException, EmissionSource>>
{
    public async Task<Either<EmissionSourceException, EmissionSource>> Handle(UpdateWaterEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEmissionSourceId(request.Id, cancellationToken)
            .BindAsync(CheckType)
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

    private Either<EmissionSourceException, WaterEmissionSource> CheckType(
        EmissionSource emissionSource)
    {
        if (emissionSource is WaterEmissionSource waterEmission)
        {
            return waterEmission;
        }

        return new EmissionSourceTypeMismatchException(emissionSource.Id, typeof(WaterEmissionSource),
            emissionSource.GetType());
    }

    private async Task<Either<EmissionSourceException, EmissionSource>> UpdateEntity(
        WaterEmissionSource entity,
        UpdateWaterEmissionSourceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(request.Receiver, request.DesignFlowRate);
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