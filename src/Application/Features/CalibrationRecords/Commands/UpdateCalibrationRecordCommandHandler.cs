using Application.Common.Interfaces.Persistence;
using Application.Features.CalibrationRecords.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.CalibrationRecords.Commands;

public class UpdateCalibrationRecordCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCalibrationRecordCommand,
        Either<CalibrationRecordException, CalibrationRecord>>
{
    public async Task<Either<CalibrationRecordException, CalibrationRecord>> Handle(
        UpdateCalibrationRecordCommand request, CancellationToken cancellationToken)
    {
        return await CheckId(request.Id, cancellationToken)
            .BindAsync(e => UpdateEntity(e, request, cancellationToken));
    }

    private async Task<Either<CalibrationRecordException, CalibrationRecord>> CheckId(
        Guid id, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.CalibrationRecordRepository.GetByIdAsync(id, cancellationToken);
        return entity.Match<Either<CalibrationRecordException, CalibrationRecord>>(
            e => e,
            () => new CalibrationRecordNotFoundException(id));
    }

    private async Task<Either<CalibrationRecordException, CalibrationRecord>> UpdateEntity(
        CalibrationRecord entity, UpdateCalibrationRecordCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            entity.UpdateDetails(request.Type, request.PerformedAt, request.NextDueAt,
                request.Result, request.PerformedBy, request.CertificateNumber, request.Notes);
            var updated = unitOfWork.CalibrationRecordRepository.Update(entity);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return updated;
        }
        catch (Exception exception)
        {
            return new UnhandledCalibrationRecordException(entity.Id, exception);
        }
    }
}
