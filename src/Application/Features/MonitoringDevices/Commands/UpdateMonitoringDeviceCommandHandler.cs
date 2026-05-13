using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringDevices.Commands;

public class UpdateMonitoringDeviceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateMonitoringDeviceCommand, Either<MonitoringDeviceException, MonitoringDevice>>
{
    public async Task<Either<MonitoringDeviceException, MonitoringDevice>> Handle(
        UpdateMonitoringDeviceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(d => CheckEmissionSourceId(d, request.EmissionSourceId, cancellationToken))
            .BindAsync(d => UpdateEntity(d, request, cancellationToken));
    }

    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> CheckId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MonitoringDeviceRepository.GetByIdAsync(id, cancellationToken);

        return entity.Match<Either<MonitoringDeviceException, MonitoringDevice>>(
            d => d,
            () => new MonitoringDeviceNotFoundException(id)
        );
    }


    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> CheckEmissionSourceId(
        MonitoringDevice device,
        Guid? emissionSourceId,
        CancellationToken cancellationToken)
    {
        if (emissionSourceId is null) return device;

        var entityOption =
            await unitOfWork.EmissionSourceRepository.GetByIdAsync(emissionSourceId.Value, cancellationToken);

        return entityOption.Match<Either<MonitoringDeviceException, MonitoringDevice>>(
            source =>
            {
                if (source.InstallationId != device.InstallationId)
                {
                    return new InvalidEmissionSourceInstallationException(
                        device.Id,
                        source.Id,
                        device.InstallationId,
                        source.InstallationId);
                }

                return device;
            },
            () => new EmissionSourceNotFoundException(device.InstallationId, emissionSourceId.Value)
        );
    }


    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> UpdateEntity(
        MonitoringDevice entity,
        UpdateMonitoringDeviceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(
                request.EmissionSourceId,
                request.IsOnline,
                request.Notes);

            var updatedMonitoringDevice = unitOfWork.MonitoringDeviceRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updatedMonitoringDevice;
        }
        catch (Exception exception)
        {
            return new UnhandledMonitoringDeviceException(entity.Id, exception);
        }
    }
}