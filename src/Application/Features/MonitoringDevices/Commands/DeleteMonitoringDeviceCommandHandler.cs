using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringDevices.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringDevices.Commands;

public class DeleteMonitoringDeviceCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteMonitoringDeviceCommand, Either<MonitoringDeviceException, MonitoringDevice>>
{
    public async Task<Either<MonitoringDeviceException, MonitoringDevice>> Handle(
        DeleteMonitoringDeviceCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(d => CheckDependencies(d, cancellationToken))
            .BindAsync(d => DeleteEntity(d, cancellationToken));
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

    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> CheckDependencies(
        MonitoringDevice entity,
        CancellationToken cancellationToken)
    {
        var hasDependencies =
            await unitOfWork.MonitoringDeviceRepository.HasDependenciesAsync(entity.Id, cancellationToken);

        return hasDependencies
            ? new MonitoringDeviceHasDependenciesException(entity.Id)
            : entity;
    }

    private async Task<Either<MonitoringDeviceException, MonitoringDevice>> DeleteEntity(
        MonitoringDevice entity,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedMonitoringDevice = unitOfWork.MonitoringDeviceRepository.Delete(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return deletedMonitoringDevice;
        }
        catch (Exception exception)
        {
            return new UnhandledMonitoringDeviceException(entity.Id, exception);
        }
    }
}