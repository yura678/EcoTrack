using Application.Common.Interfaces.Persistence;
using Application.Features.DevicePollutantCapabilities.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.DevicePollutantCapabilities.Commands;

public class CreateDevicePollutantCapabilityCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateDevicePollutantCapabilityCommand,
        Either<DevicePollutantCapabilityException, DevicePollutantCapability>>
{
    public async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> Handle(
        CreateDevicePollutantCapabilityCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckDevice(request.DeviceId, cancellationToken)
            .BindAsync(_ => CheckPollutant(request.PollutantId, cancellationToken))
            .BindAsync(_ => CheckUnit(request.RangeUnitId, cancellationToken))
            .BindAsync(_ => CheckDuplicate(request.DeviceId, request.PollutantId, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<DevicePollutantCapabilityException, Unit>> CheckDevice(
        Guid deviceId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MonitoringDeviceRepository.GetByIdAsync(deviceId, cancellationToken);
        return entity.Match<Either<DevicePollutantCapabilityException, Unit>>(
            _ => Unit.Default,
            () => new CapabilityDeviceNotFoundException(deviceId));
    }

    private async Task<Either<DevicePollutantCapabilityException, Unit>> CheckPollutant(
        Guid pollutantId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PollutantRepository.GetByIdAsync(pollutantId, cancellationToken);
        return entity.Match<Either<DevicePollutantCapabilityException, Unit>>(
            _ => Unit.Default,
            () => new CapabilityPollutantNotFoundException(pollutantId));
    }

    private async Task<Either<DevicePollutantCapabilityException, Unit>> CheckUnit(
        Guid unitId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MeasureUnitRepository.GetByIdAsync(unitId, cancellationToken);
        return entity.Match<Either<DevicePollutantCapabilityException, Unit>>(
            _ => Unit.Default,
            () => new CapabilityMeasureUnitNotFoundException(unitId));
    }

    private async Task<Either<DevicePollutantCapabilityException, Unit>> CheckDuplicate(
        Guid deviceId, Guid pollutantId, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.DevicePollutantCapabilityRepository
            .GetByDeviceAndPollutantAsync(deviceId, pollutantId, cancellationToken);
        return entity.Match<Either<DevicePollutantCapabilityException, Unit>>(
            _ => new CapabilityAlreadyExistsException(deviceId, pollutantId),
            () => Unit.Default);
    }

    private async Task<Either<DevicePollutantCapabilityException, DevicePollutantCapability>> CreateEntity(
        CreateDevicePollutantCapabilityCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var newEntity = await unitOfWork.DevicePollutantCapabilityRepository.AddAsync(
                DevicePollutantCapability.New(Guid.NewGuid(),
                    request.DeviceId, request.PollutantId,
                    request.RangeMin, request.RangeMax, request.RangeUnitId,
                    request.AccuracyClass, request.ExpectedIntervalMinutes),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return newEntity;
        }
        catch (Exception exception)
        {
            return new UnhandledDevicePollutantCapabilityException(Guid.Empty, exception);
        }
    }
}
