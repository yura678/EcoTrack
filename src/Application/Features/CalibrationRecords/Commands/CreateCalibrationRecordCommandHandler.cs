using Application.Common.Interfaces.Persistence;
using Application.Features.CalibrationRecords.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.CalibrationRecords.Commands;

public class CreateCalibrationRecordCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCalibrationRecordCommand,
        Either<CalibrationRecordException, CalibrationRecord>>
{
    public async Task<Either<CalibrationRecordException, CalibrationRecord>> Handle(
        CreateCalibrationRecordCommand request, CancellationToken cancellationToken)
    {
        return await CheckDevice(request.DeviceId, cancellationToken)
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }

    private async Task<Either<CalibrationRecordException, Unit>> CheckDevice(Guid deviceId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MonitoringDeviceRepository.GetByIdAsync(deviceId, cancellationToken);
        return entity.Match<Either<CalibrationRecordException, Unit>>(
            _ => Unit.Default,
            () => new CalibrationDeviceNotFoundException(deviceId));
    }

    private async Task<Either<CalibrationRecordException, CalibrationRecord>> CreateEntity(
        CreateCalibrationRecordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var newEntity = await unitOfWork.CalibrationRecordRepository.AddAsync(
                CalibrationRecord.New(Guid.NewGuid(), request.DeviceId, request.Type,
                    request.PerformedAt, request.NextDueAt, request.Result,
                    request.PerformedBy, request.CertificateNumber, request.Notes),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return newEntity;
        }
        catch (Exception exception)
        {
            return new UnhandledCalibrationRecordException(Guid.Empty, exception);
        }
    }
}
