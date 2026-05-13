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
            if (result.IsLeft) transaction.Rollback();
            else transaction.Commit();
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
        entity.MarkQuality(Quality.Invalid, request.Reason);
        unitOfWork.MeasurementRepository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateLinkedComplianceEvents(entity.Id, cancellationToken);

        return entity;
    }

    private async Task InvalidateLinkedComplianceEvents(
        Guid measurementId,
        CancellationToken cancellationToken)
    {
        var events =
            await unitOfWork.ComplianceEventRepository.GetByMeasurementIdAsync(measurementId, cancellationToken);

        foreach (var ev in events)
        {
            if (ev.Status != ComplianceEventStatus.Closed)
            {
                ev.ChangeStatus(ComplianceEventStatus.Closed);

                unitOfWork.ComplianceEventRepository.Update(ev);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
