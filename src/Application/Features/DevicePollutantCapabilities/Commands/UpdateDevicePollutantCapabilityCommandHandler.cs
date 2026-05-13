using Application.Common.Interfaces.Persistence;
using Application.Features.DevicePollutantCapabilities.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.DevicePollutantCapabilities.Commands;

public class UpdateDevicePollutantCapabilityCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDevicePollutantCapabilityCommand,
        Either<DevicePollutantCapabilityException, DevicePollutantCapability>>
{
    public async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> Handle(
        UpdateDevicePollutantCapabilityCommand request, CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(e => CheckUnit(e, request.RangeUnitId, cancellationToken))
            .BindAsync(e => UpdateEntity(e, request, cancellationToken));
    }

    private async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> CheckId(
        Guid id, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.DevicePollutantCapabilityRepository.GetByIdAsync(id, cancellationToken);
        return entity.Match<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>(
            e => e,
            () => new DevicePollutantCapabilityNotFoundException(id));
    }

    private async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> CheckUnit(
        DevicePollutantCapability capability, Guid unitId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MeasureUnitRepository.GetByIdAsync(unitId, cancellationToken);
        return entity.Match<Either<DevicePollutantCapabilityException, DevicePollutantCapability>>(
            _ => capability,
            () => new CapabilityMeasureUnitNotFoundException(unitId));
    }

    private async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> UpdateEntity(
        DevicePollutantCapability entity,
        UpdateDevicePollutantCapabilityCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(request.RangeMin, request.RangeMax, request.RangeUnitId,
                request.AccuracyClass);
            var updated = unitOfWork.DevicePollutantCapabilityRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updated;
        }
        catch (Exception exception)
        {
            return new UnhandledDevicePollutantCapabilityException(entity.Id, exception);
        }
    }
}
