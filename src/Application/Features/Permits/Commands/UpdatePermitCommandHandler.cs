using Application.Common.Interfaces.Persistence;
using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;

namespace Application.Features.Permits.Commands;

public class UpdatePermitCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePermitCommand, Either<PermitException, Permit>>
{
    public async Task<Either<PermitException, Permit>> Handle(
        UpdatePermitCommand request,
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
            return new UnhandledPermitException(request.Id, exception);
        }
    }

    private async Task<Either<PermitException, Permit>> HandleAsync(
        UpdatePermitCommand request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> pollutantIds = request.EmissionLimits
            .Select(r => r.PollutantId)
            .Distinct().ToList();

        IReadOnlyList<Guid> emissionSourceIds = request.EmissionLimits
            .Select(r => r.EmissionSourceId)
            .Distinct().ToList();

        IReadOnlyList<Guid> unitIds = request.EmissionLimits
            .Select(r => r.UnitId)
            .Distinct().ToList();

        return await CheckPermitId(request.Id, cancellationToken)
            .BindAsync(p => CheckPollutantIds(p, pollutantIds, cancellationToken))
            .BindAsync(p => CheckEmissionSourceIds(p, emissionSourceIds, cancellationToken))
            .BindAsync(p => CheckMeasureUnitIds(p, unitIds, cancellationToken))
            .BindAsync(p => CheckLimitDateRanges(p, request))
            .BindAsync(p => UpdateEntity(p, request, cancellationToken));
    }

    private async Task<Either<PermitException, Permit>> CheckPermitId(
        Guid permitId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PermitRepository.GetByIdAsync(permitId, cancellationToken);

        return entity.Match<Either<PermitException, Permit>>(
            p =>
            {
                if (p.PermitStatus != PermitStatus.Draft)
                {
                    return new PermitInvalidStatusException(
                        permitId,
                        p.PermitStatus,
                        "Only Draft permit can be modified.");
                }

                return p;
            },
            () => new PermitNotFoundException(permitId)
        );
    }

    private async Task<Either<PermitException, Permit>> CheckMeasureUnitIds(
        Permit entity,
        IReadOnlyList<Guid> measurementUnitIds,
        CancellationToken cancellationToken)
    {
        var entities = await unitOfWork.MeasureUnitRepository.GetByIdsAsync(measurementUnitIds, cancellationToken);

        IReadOnlyList<Guid> missingMeasureUnits =
            measurementUnitIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingMeasureUnits.Any()
            ? new MeasureUnitNotFoundException(entity.Id, missingMeasureUnits)
            : entity;
    }

    private async Task<Either<PermitException, Permit>> CheckPollutantIds(
        Permit entity,
        IReadOnlyList<Guid> pollutantIds,
        CancellationToken cancellationToken)
    {
        var entities = await unitOfWork.PollutantRepository.GetByIdsAsync(pollutantIds, cancellationToken);

        IReadOnlyList<Guid> missingPollutants =
            pollutantIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingPollutants.Any()
            ? new PollutantNotFoundException(entity.Id, missingPollutants)
            : entity;
    }


    private async Task<Either<PermitException, Permit>> CheckEmissionSourceIds(
        Permit entity,
        IReadOnlyList<Guid> emissionSourceIds,
        CancellationToken cancellationToken)
    {
        var entities =
            await unitOfWork.EmissionSourceRepository.GetByInstallationAndIdsAsync(entity.InstallationId,
                emissionSourceIds,
                cancellationToken);

        IReadOnlyList<Guid> missingEmissionSources =
            emissionSourceIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingEmissionSources.Any()
            ? new EmissionSourceNotFoundException(entity.Id, missingEmissionSources)
            : entity;
    }

    private Either<PermitException, Permit> CheckLimitDateRanges(
        Permit entity,
        UpdatePermitCommand request)
    {
        foreach (var limit in request.EmissionLimits)
        {
            var effectiveFrom = limit.ValidFrom ?? request.IssuedAt;
            var effectiveTo = limit.ValidTo ?? request.ValidUntil;

            if (effectiveFrom > effectiveTo)
            {
                return new InvalidEmissionLimitDateRangeException(
                    entity.Id,
                    $"Emission limit has invalid date range: {effectiveFrom} > {effectiveTo}");
            }

            if (effectiveFrom < request.IssuedAt)
            {
                return new InvalidEmissionLimitDateRangeException(
                    entity.Id,
                    $"Emission limit cannot begin before Permit.IssuedAt ({request.IssuedAt}).");
            }

            if (effectiveTo > request.ValidUntil)
            {
                return new InvalidEmissionLimitDateRangeException(
                    entity.Id,
                    $"Emission limit cannot end after Permit.ValidUntil ({request.ValidUntil}).");
            }
        }

        return entity;
    }


    private async Task<Either<PermitException, Permit>> UpdateEntity(
        Permit permit,
        UpdatePermitCommand request,
        CancellationToken cancellationToken)
    {
        var dtoLimits = request.EmissionLimits;

        // Мапимо існуючі за Id
        Dictionary<Guid, EmissionLimit> existingMap = permit.EmissionLimits
            .ToDictionary(x => x.Id, x => x);

        var toRemove = new List<EmissionLimit>();
        var toAdd = new List<EmissionLimit>();
        var toUpdate = new List<EmissionLimit>();

        foreach (UpdateEmissionLimitCommandDto dto in dtoLimits)
        {
            if (dto.Id is null)
            {
                // New
                toAdd.Add(EmissionLimit.New(
                    Guid.NewGuid(),
                    dto.Value,
                    dto.Period,
                    permit.Id,
                    dto.UnitId,
                    dto.PollutantId,
                    dto.EmissionSourceId,
                    dto.ValidFrom,
                    dto.ValidTo
                ));
                continue;
            }

            // dto.Id != null → треба знайти існуючий EmissionLimit
            if (!existingMap.TryGetValue(dto.Id.Value, out EmissionLimit? entity))
            {
                return new EmissionLimitNotFoundException(permit.Id, dto.Id.Value);
            }

            entity.UpdateDetails(
                dto.Value,
                dto.Period,
                dto.UnitId,
                dto.PollutantId,
                dto.EmissionSourceId,
                dto.ValidFrom,
                dto.ValidTo);
            toUpdate.Add(entity);

            existingMap.Remove(dto.Id.Value); // залишок — ті, що треба видалити
        }

        toRemove.AddRange(existingMap.Values);

        if (toRemove.Any())
        {
            unitOfWork.EmissionLimitRepository.RemoveRange(toRemove);
        }

        if (toUpdate.Any())
        {
            unitOfWork.EmissionLimitRepository.UpdateRange(toUpdate);
        }

        if (toAdd.Any())
        {
            await unitOfWork.EmissionLimitRepository.AddRangeAsync(toAdd, cancellationToken);
        }

        permit.UpdateDetails(
            request.Number,
            request.PermitType,
            request.IssuedAt,
            request.ValidUntil,
            request.Authority,
            request.Notes);

        var updated = unitOfWork.PermitRepository.Update(permit);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return updated;
    }
}