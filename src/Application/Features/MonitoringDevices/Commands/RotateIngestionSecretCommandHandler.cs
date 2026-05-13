using System.Security.Cryptography;
using Application.Common.Interfaces.Persistence;
using Application.Features.MonitoringDevices.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.MonitoringDevices.Commands;

public class RotateIngestionSecretCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<RotateIngestionSecretCommand,
        Either<MonitoringDeviceException, RotateIngestionSecretResult>>
{
    public async Task<Either<MonitoringDeviceException, RotateIngestionSecretResult>> Handle(
        RotateIngestionSecretCommand request, CancellationToken cancellationToken)
    {
        var deviceOption = await unitOfWork.MonitoringDeviceRepository
            .GetByIdAsync(request.DeviceId, cancellationToken);

        return await deviceOption.MatchAsync(
            async device =>
            {
                try
                {
                    var bytes = RandomNumberGenerator.GetBytes(32);
                    var secret = Convert.ToBase64String(bytes);
                    device.RotateIngestionSecret(secret);
                    unitOfWork.MonitoringDeviceRepository.Update(device);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return (Either<MonitoringDeviceException, RotateIngestionSecretResult>)
                        new RotateIngestionSecretResult(device.Id, secret);
                }
                catch (Exception exception)
                {
                    return new UnhandledMonitoringDeviceException(device.Id, exception);
                }
            },
            () => Task.FromResult<Either<MonitoringDeviceException, RotateIngestionSecretResult>>(
                new MonitoringDeviceNotFoundException(request.DeviceId))
        );
    }
}
