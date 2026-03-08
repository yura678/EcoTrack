using Application.Common.Interfaces.Persistence;
using Application.Features.Measurements.Exceptions;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Measurements.Command;

public class CreateMeasurementCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateMeasurementCommand,
        Either<MeasurementException, Measurement>>
{
    public async Task<Either<MeasurementException, Measurement>> Handle(
        CreateMeasurementCommand request,
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
        CreateMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEmissionSourceId(request.EmissionSourceId, cancellationToken)
            .BindAsync(r => CheckPollutantId(request.PollutantId, cancellationToken))
            .BindAsync(_ => CheckIfRequirementExist(request, cancellationToken))
            .BindAsync(r => ValidateAveragingWindow(r, request.Period, request.EmissionSourceId, request.PollutantId))
            .BindAsync(_ => CheckMonitoringDeviceId(request.DeviceId, request.EmissionSourceId, cancellationToken))
            .BindAsync(_ => CheckMeasureUnitId(request.UnitId, cancellationToken))
            .BindAsync(u => CheckForDuplicateMeasurement(u, request.Timestamp, request.PollutantId,
                request.EmissionSourceId, cancellationToken))
            .BindAsync(u => CreateEntity(u, request, cancellationToken));
    }


    private async Task<Either<MeasurementException, Unit>> CheckEmissionSourceId(
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.EmissionSourceRepository.GetByIdAsync(emissionSourceId, cancellationToken);

        return entity.Match<Either<MeasurementException, Unit>>(
            _ => Unit.Default,
            () => new MeasurementRelatedEntityNotFoundException(Guid.Empty, typeof(EmissionSource),
                emissionSourceId)
        );
    }

    private async Task<Either<MeasurementException, Unit>> CheckPollutantId(
        Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PollutantRepository.GetByIdAsync(pollutantId, cancellationToken);

        return entity.Match<Either<MeasurementException, Unit>>(
            _ => Unit.Default,
            () => new MeasurementRelatedEntityNotFoundException(Guid.Empty, typeof(Pollutant),
                pollutantId)
        );
    }

    private async Task<Either<MeasurementException, Unit>> CheckMonitoringDeviceId(
        Guid deviceId,
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        var entity =
            await unitOfWork.MonitoringDeviceRepository.GetByIdAsync(emissionSourceId, deviceId, cancellationToken);

        return entity.Match<Either<MeasurementException, Unit>>(
            _ => Unit.Default,
            () => new MeasurementRelatedEntityNotFoundException(Guid.Empty, typeof(MonitoringDevice),
                deviceId)
        );
    }

    private async Task<Either<MeasurementException, MeasureUnit>> CheckMeasureUnitId(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MeasureUnitRepository.GetByIdAsync(unitId, cancellationToken);

        return entity.Match<Either<MeasurementException, MeasureUnit>>(
            u => u,
            () => new MeasurementRelatedEntityNotFoundException(Guid.Empty, typeof(MeasureUnit),
                unitId)
        );
    }

    private async Task<Either<MeasurementException, MonitoringRequirement>> CheckIfRequirementExist(
        CreateMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        var monitoringPlan = await unitOfWork.MonitoringPlanRepository.GetActiveByEmissionSourceAsync(
            request.EmissionSourceId, cancellationToken);

        return monitoringPlan.Match<Either<MeasurementException, MonitoringRequirement>>(
            m =>
            {
                var requirement = m.Requirements!.FirstOrDefault(x => x.PollutantId.Equals(request.PollutantId));
                if (requirement is null)
                {
                    return new MonitoringRequirementNotFoundException(Guid.Empty,
                        request.EmissionSourceId, request.PollutantId);
                }

                return requirement;
            },
            () => new MonitoringRequirementNotFoundException(Guid.Empty,
                request.EmissionSourceId, request.PollutantId)
        );
    }


    private Either<MeasurementException, Unit> ValidateAveragingWindow(
        MonitoringRequirement requirement,
        AveragingWindow window,
        Guid sourceId,
        Guid pollutantId)
    {
        return requirement.Frequency switch
        {
            Frequency.Hourly when window != AveragingWindow.OneHour => new InvalidAveragingWindowException(
                Guid.Empty, sourceId, pollutantId, expected: AveragingWindow.OneHour,
                actual: window),
            Frequency.Daily when window != AveragingWindow.TwentyFourHours => new
                InvalidAveragingWindowException(Guid.Empty, sourceId, pollutantId,
                    expected: AveragingWindow.TwentyFourHours, actual: window),
            _ => Unit.Default
        };
    }

    private async Task<Either<MeasurementException, MeasureUnit>> CheckForDuplicateMeasurement(
        MeasureUnit measureUnit,
        DateTime timestamp,
        Guid pollutantId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.MeasurementRepository.GetByTimeStamp(
            timestamp,
            pollutantId,
            sourceId,
            cancellationToken);

        return entity.Match<Either<MeasurementException, MeasureUnit>>(
            _ => new DuplicateMeasurementException(Guid.Empty, sourceId, pollutantId, timestamp),
            () => measureUnit
        );
    }


    private async Task<IReadOnlyCollection<EmissionLimit>> GetApplicableLimits(
        MeasureUnit measureUnit,
        CreateMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        var permitOption = await unitOfWork.PermitRepository.GetActiveByEmissionSourceAsync(
            request.EmissionSourceId,
            request.Timestamp,
            cancellationToken
        );

        return permitOption.Match<IReadOnlyCollection<EmissionLimit>>(
            permit => FilterLimits(permit, measureUnit, request),
            () => []
        );
    }

    private IReadOnlyCollection<EmissionLimit> FilterLimits(
        Permit permit,
        MeasureUnit measureUnit,
        CreateMeasurementCommand request)
    {
        return permit.EmissionLimits!
            .Where(l =>
                l.EmissionSourceId.Equals(request.EmissionSourceId) &&
                l.PollutantId.Equals(request.PollutantId) &&
                l.Period == request.Period &&
                l.Unit.Dimension == measureUnit.Dimension &&
                (l.ValidFrom == null || l.ValidFrom <= request.Timestamp) &&
                (l.ValidTo == null || l.ValidTo >= request.Timestamp)
            )
            .ToList();
    }

    private async Task<Either<MeasurementException, Measurement>> CreateEntity(
        MeasureUnit measureUnit,
        CreateMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        var newMeasurement = Measurement.New(
            Guid.NewGuid(),
            request.Timestamp,
            request.EmissionSourceId,
            request.PollutantId,
            request.DeviceId,
            request.UnitId,
            request.Period,
            request.Value,
            null
        );

        var applicableLimits = await GetApplicableLimits(
            measureUnit,
            request,
            cancellationToken
        );

        var measurementBaseValue = newMeasurement.Value * measureUnit.ToBaseFactor;

        var exceedanceEvents =
            applicableLimits
                .Where(limit => measurementBaseValue > limit.Value * limit.Unit.ToBaseFactor)
                .Select(limit =>
                {
                    var measurementInLimitUnits = measurementBaseValue / limit.Unit.ToBaseFactor;
                    var magnitude = measurementInLimitUnits - limit.Value;

                    return ExceedanceEvent.New(
                        Guid.NewGuid(),
                        newMeasurement.Id,
                        limit.Id,
                        magnitude,
                        ExceedanceEventStatus.Open,
                        notes:
                        $"Measured {measurementInLimitUnits} {limit.Unit.Symbol} > Limit {limit.Value} {limit.Unit.Symbol}"
                    );
                })
                .ToList();

        var addedMeasurement = await unitOfWork.MeasurementRepository.AddAsync(newMeasurement, cancellationToken);
        await unitOfWork.ExceedanceEventRepository.AddRangeAsync(exceedanceEvents, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);


        return addedMeasurement;
    }
}