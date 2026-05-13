using Application.Common.Interfaces.Persistence;
using Application.Features.CalibrationRecords.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.CalibrationRecords.Commands;

public class DeleteCalibrationRecordCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteCalibrationRecordCommand,
        Either<CalibrationRecordException, CalibrationRecord>>
{
    public async Task<Either<CalibrationRecordException, CalibrationRecord>> Handle(
        DeleteCalibrationRecordCommand request, CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.CalibrationRecordRepository
            .GetByIdAsync(request.Id, cancellationToken);

        return await entity.Match<Task<Either<CalibrationRecordException, CalibrationRecord>>>(
            async e =>
            {
                try
                {
                    var deleted = unitOfWork.CalibrationRecordRepository.Delete(e);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return deleted;
                }
                catch (Exception exception)
                {
                    return new UnhandledCalibrationRecordException(e.Id, exception);
                }
            },
            () => Task.FromResult<Either<CalibrationRecordException, CalibrationRecord>>(
                new CalibrationRecordNotFoundException(request.Id))
        );
    }
}
