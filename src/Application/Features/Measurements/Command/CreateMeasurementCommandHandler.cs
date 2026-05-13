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
        CreateMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckEmissionSourceId(request.EmissionSourceId, cancellationToken)
            .BindAsync(r => CheckPollutantId(request.PollutantId, cancellationToken))
            .BindAsync(_ => CheckIfRequirementExist(request, cancellationToken))
            .BindAsync(r => ValidateAveragingWindow(r, request.Window, request.EmissionSourceId, request.PollutantId))
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
            Frequency.Hourly when window != AveragingWindow.Hour1 => new InvalidAveragingWindowException(
                Guid.Empty, sourceId, pollutantId, expected: AveragingWindow.Hour1,
                actual: window),
            Frequency.Daily when window != AveragingWindow.Hour24 => new
                InvalidAveragingWindowException(Guid.Empty, sourceId, pollutantId,
                    expected: AveragingWindow.Hour24, actual: window),
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
                (l.EmissionSourceId == request.EmissionSourceId) &&
                l.PollutantId.Equals(request.PollutantId) &&
                l.Period == request.Window &&
                l.Unit!.Dimension == measureUnit.Dimension &&
                l.ValidFrom <= request.Timestamp &&
                (l.ValidTo == null || l.ValidTo >= request.Timestamp)
            )
            .ToList();
    }

    private static (DateTime start, DateTime end) ComputeWindowBounds(DateTime timestamp,
        AveragingWindow window)
    {
        TimeSpan duration = window switch
        {
            AveragingWindow.Minute1 => TimeSpan.FromMinutes(1),
            AveragingWindow.Minute10 => TimeSpan.FromMinutes(10),
            AveragingWindow.HalfHour => TimeSpan.FromMinutes(30),
            AveragingWindow.Hour1 => TimeSpan.FromHours(1),
            AveragingWindow.Hour24 => TimeSpan.FromHours(24),
            AveragingWindow.Month1 => TimeSpan.FromDays(30),
            AveragingWindow.Year1 => TimeSpan.FromDays(365),
            _ => TimeSpan.FromHours(1)
        };
        return (timestamp - duration, timestamp);
    }

    private async Task<Either<MeasurementException, Measurement>> CreateEntity(
        MeasureUnit measureUnit,
        CreateMeasurementCommand request,
        CancellationToken cancellationToken)
    {
        var (windowStart, windowEnd) = ComputeWindowBounds(request.Timestamp, request.Window);

        var newMeasurement = Measurement.New(
            id: Guid.NewGuid(),
            windowStart: windowStart,
            windowEnd: windowEnd,
            window: request.Window,
            aggregation: Aggregation.Average,
            emissionSourceId: request.EmissionSourceId,
            pollutantId: request.PollutantId,
            deviceId: request.DeviceId,
            unitId: request.UnitId,
            value: request.Value,
            validPointsCount: 1,
            expectedPointsCount: 1);

        var applicableLimits = await GetApplicableLimits(
            measureUnit,
            request,
            cancellationToken
        );

        var measurementBaseValue = newMeasurement.Value * measureUnit.ToBaseFactor;

        var complianceEvents =
            applicableLimits
                .Where(limit => measurementBaseValue > limit.Value * limit.Unit!.ToBaseFactor)
                .Select(limit =>
                {
                    var measurementInLimitUnits = measurementBaseValue / limit.Unit!.ToBaseFactor;
                    var ratio = measurementInLimitUnits / limit.Value;

                    return ComplianceEvent.ForLimitExceedance(
                        id: Guid.NewGuid(),
                        emissionSourceId: request.EmissionSourceId,
                        measurementId: newMeasurement.Id,
                        limitId: limit.Id,
                        ratio: ratio,
                        windowStart: windowStart,
                        windowEnd: windowEnd,
                        notes:
                        $"Measured {measurementInLimitUnits} {limit.Unit.Symbol} > Limit {limit.Value} {limit.Unit.Symbol}"
                    );
                })
                .ToList();

        var addedMeasurement = await unitOfWork.MeasurementRepository.AddAsync(newMeasurement, cancellationToken);
        await unitOfWork.ComplianceEventRepository.AddRangeAsync(complianceEvents, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);


        return addedMeasurement;
    }
}
