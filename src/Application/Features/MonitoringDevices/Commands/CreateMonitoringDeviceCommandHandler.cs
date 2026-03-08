using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.MonitoringDevices.Commands;

public class CreateMonitoringDeviceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateMonitoringDeviceCommand, Either<MonitoringDeviceException, MonitoringDevice>>
{
    public async Task<Either<MonitoringDeviceException, MonitoringDevice>> Handle(
        CreateMonitoringDeviceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEmissionSourceId(request.EmissionSourceId, cancellationToken)
            .BindAsync(_ => CheckInstallationId(request.InstallationId, cancellationToken))
            .BindAsync(_ => CheckMonitoringDeviceSerialNumber(request.SerialNumber, cancellationToken))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<MonitoringDeviceException, Unit>> CheckEmissionSourceId(
        Guid? emissionSourceId,
        CancellationToken cancellationToken)
    {
        if (emissionSourceId is null) return Unit.Default;
        var entity = await unitOfWork.EmissionSourceRepository.GetByIdAsync(emissionSourceId.Value, cancellationToken);

        return entity.Match<Either<MonitoringDeviceException, Unit>>(
            _ => Unit.Default,
            () => new EmissionSourceNotFoundException(Guid.Empty, emissionSourceId.Value)
        );
    }

    private async Task<Either<MonitoringDeviceException, Unit>> CheckInstallationId(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InstallationRepository.GetByIdAsync(installationId, cancellationToken);

        return entity.Match<Either<MonitoringDeviceException, Unit>>(
            _ => Unit.Default,
            () => new InstallationNotFoundException(Guid.Empty, installationId)
        );
    }


    private async Task<Either<MonitoringDeviceException, Unit>> CheckMonitoringDeviceSerialNumber(
        string serialNumber,
        CancellationToken cancellationToken)
    {
        var entity =
            await unitOfWork.MonitoringDeviceRepository.GetBySerialNumberAsync(serialNumber, cancellationToken);

        return entity.Match<Either<MonitoringDeviceException, Unit>>(
            _ => new MonitoringDeviceNumberAlreadyExistsException(Guid.Empty, serialNumber),
            () => Unit.Default
        );
    }

    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> CreateEntity(
        CreateMonitoringDeviceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newMonitoringDevice = await unitOfWork.MonitoringDeviceRepository.AddAsync(
                MonitoringDevice.New(
                    Guid.NewGuid(),
                    request.InstallationId,
                    request.EmissionSourceId,
                    request.Model,
                    request.SerialNumber,
                    request.Type,
                    request.IsOnline,
                    request.Notes), cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return newMonitoringDevice;
        }
        catch (Exception exception)
        {
            return new UnhandledMonitoringDeviceException(Guid.Empty, exception);
        }
    }
}