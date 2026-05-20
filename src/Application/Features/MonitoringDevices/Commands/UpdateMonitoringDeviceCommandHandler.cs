using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Enterprises;
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
            .BindAsync(d => CheckParentInstallationActiveForStatus(d, request.Status, cancellationToken))
            .BindAsync(d => UpdateEntity(d, request, cancellationToken));
    }

    /// <summary>
    /// Refuse to take a device out of Decommissioned while its parent installation is itself
    /// Decommissioned. Without this guard the operator could flip the device's status and
    /// create a ghost configuration where the device would happily ingest into raw_measurement
    /// past a decommissioned installation — data nobody can see through the heatmap (filter
    /// hides decommissioned installations) but that still consumes write throughput.
    /// </summary>
    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> CheckParentInstallationActiveForStatus(
        MonitoringDevice device,
        DeviceStatus requestedStatus,
        CancellationToken cancellationToken)
    {
        if (requestedStatus == DeviceStatus.Decommissioned) return device;

        var installationOption = await unitOfWork.InstallationRepository
            .GetByIdAsync(device.InstallationId, cancellationToken);

        return installationOption.Match<Either<MonitoringDeviceException, MonitoringDevice>>(
            Some: i => i.Status == InstallationStatus.Decommissioned
                ? new ParentInstallationDecommissionedException(device.Id, i.Id)
                : device,
            None: () => new InstallationNotFoundException(device.Id, device.InstallationId));
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
                request.Status,
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