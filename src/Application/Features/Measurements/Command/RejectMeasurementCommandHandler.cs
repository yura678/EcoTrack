using Application.Common.Interfaces.Persistence;
using Application.Features.Measurements.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.Measurements.Command;



public class RejectMeasurementCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<RejectMeasurementCommand, Either<MeasurementException, Measurement>>
{
    public async Task<Either<MeasurementException, Measurement>> Handle(
        RejectMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await HandleAsync(request, cancellationToken);
            if (result.IsLeft)
            {
                transaction.Rollback();
            }
            else
            {
                transaction.Commit();
            }

            return result;
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return new UnhandledMeasurementException(Guid.Empty, exception);
        }
    }

    private async Task<Either<MeasurementException, Measurement>> HandleAsync(
        RejectMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckMeasurementId(request.Id, cancellationToken)
            .BindAsync(m => UpdateEntity(m, request, cancellationToken));
    }


    private async Task<Either<MeasurementException, Measurement>> CheckMeasurementId(
        Guid measurementId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MeasurementRepository.GetByIdAsync(measurementId, cancellationToken);

        return entity.Match<Either<MeasurementException, Measurement>>(
            m => m,
            () => new MeasurementNotFoundException(measurementId)
        );
    }

    private async Task<Either<MeasurementException, Measurement>> UpdateEntity(
        Measurement entity,
        RejectMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        entity.ChangeStatus(MeasurementStatus.Rejected, request.Reason);
        unitOfWork.MeasurementRepository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateLinkedExceedanceEvent(entity.Id, cancellationToken);

        return entity;
    }

    private async Task InvalidateLinkedExceedanceEvent(
        Guid measurementId,
        CancellationToken cancellationToken)
    {
        var exceedanceEvents =
            await unitOfWork.ExceedanceEventRepository.GetByMeasurementIdAsync(measurementId, cancellationToken);

        foreach (var exceedanceEvent in exceedanceEvents)
        {
            if (exceedanceEvent.Status != ExceedanceEventStatus.Closed)
            {
                exceedanceEvent.ChangeStatus(ExceedanceEventStatus.Closed);

                unitOfWork.ExceedanceEventRepository.Update(exceedanceEvent);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}