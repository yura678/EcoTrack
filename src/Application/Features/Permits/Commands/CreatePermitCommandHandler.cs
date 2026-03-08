using Application.Common.Interfaces.Persistence;
using Application.Features.Permits.Exceptions;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;
using Unit = LanguageExt.Unit;

namespace Application.Features.Permits.Commands;


public class CreatePermitCommandHandler(
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePermitCommand, Either<PermitException, Permit>>
{
    public async Task<Either<PermitException, Permit>> Handle(
        CreatePermitCommand request,
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

        return await CheckInstallationId(request.InstallationId, cancellationToken)
            .BindAsync(_ => CheckPermitNumber(request.Number, cancellationToken))
            .BindAsync(_ => CheckPollutantIds(pollutantIds, cancellationToken))
            .BindAsync(_ => CheckEmissionSourceIds(request.InstallationId, emissionSourceIds, cancellationToken))
            .BindAsync(_ => CheckMeasureUnitIds(unitIds, cancellationToken))
            .BindAsync(_ => CheckLimitDateRanges(request))
            .BindAsync(_ => CreateEntity(request, cancellationToken));
    }


    private async Task<Either<PermitException, Unit>> CheckPermitNumber(
        string number,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.PermitRepository.GetByNumberAsync(number, cancellationToken);

        return entity.Match<Either<PermitException, Unit>>(
            _ => new PermitNumberAlreadyExistsException(Guid.Empty, number),
            () => Unit.Default
        );
    }

    private async Task<Either<PermitException, Unit>> CheckInstallationId(
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var entity = await unitOfWork.InstallationRepository.GetByIdAsync(installationId, cancellationToken);

        return entity.Match<Either<PermitException, Unit>>(
            _ => Unit.Default,
            () => new InstallationNotFoundException(Guid.Empty, installationId)
        );
    }

    private async Task<Either<PermitException, Unit>> CheckPollutantIds(
        IReadOnlyList<Guid> pollutantIds,
        CancellationToken cancellationToken)
    {
        var entities = await unitOfWork.PollutantRepository.GetByIdsAsync(pollutantIds, cancellationToken);

        IReadOnlyList<Guid> missingPollutants =
            pollutantIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingPollutants.Any()
            ? new PollutantNotFoundException(Guid.Empty, missingPollutants)
            : Unit.Default;
    }


    private async Task<Either<PermitException, Unit>> CheckEmissionSourceIds(
        Guid installationId,
        IReadOnlyList<Guid> emissionSourceIds,
        CancellationToken cancellationToken)
    {
        var entities =
            await unitOfWork.EmissionSourceRepository.GetByInstallationAndIdsAsync(installationId, emissionSourceIds,
                cancellationToken);

        IReadOnlyList<Guid> missingEmissionSources =
            emissionSourceIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingEmissionSources.Any()
            ? new EmissionSourceNotFoundException(Guid.Empty, missingEmissionSources)
            : Unit.Default;
    }

    private async Task<Either<PermitException, Unit>> CheckMeasureUnitIds(
        IReadOnlyList<Guid> measurementUnitIds,
        CancellationToken cancellationToken)
    {
        var entities = await unitOfWork.MeasureUnitRepository.GetByIdsAsync(measurementUnitIds, cancellationToken);

        IReadOnlyList<Guid> missingMeasureUnits =
            measurementUnitIds.Where(x => !entities.Select(e => e.Id).Contains(x)).ToList();

        return missingMeasureUnits.Any()
            ? new MeasureUnitNotFoundException(Guid.Empty, missingMeasureUnits)
            : Unit.Default;
    }

    private Either<PermitException, Unit> CheckLimitDateRanges(CreatePermitCommand request)
    {
        foreach (var limit in request.EmissionLimits)
        {
            var effectiveFrom = limit.ValidFrom ?? request.IssuedAt;
            var effectiveTo = limit.ValidTo ?? request.ValidUntil;

            if (effectiveFrom > effectiveTo)
            {
                return new InvalidEmissionLimitDateRangeException(
                    Guid.Empty,
                    $"Emission limit has invalid date range: {effectiveFrom} > {effectiveTo}");
            }

            if (effectiveFrom < request.IssuedAt)
            {
                return new InvalidEmissionLimitDateRangeException(
                    Guid.Empty,
                    $"Emission limit cannot begin before Permit.IssuedAt ({request.IssuedAt}).");
            }

            if (effectiveTo > request.ValidUntil)
            {
                return new InvalidEmissionLimitDateRangeException(
                    Guid.Empty,
                    $"Emission limit cannot end after Permit.ValidUntil ({request.ValidUntil}).");
            }
        }

        return Unit.Default;
    }


    private async Task<Either<PermitException, Permit>> CreateEntity(
        CreatePermitCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var newPermitId = Guid.NewGuid();

            var emissionLimits = request.EmissionLimits
                .Select(dto => EmissionLimit.New(
                    Guid.NewGuid(),
                    dto.Value,
                    dto.Period,
                    newPermitId,
                    dto.UnitId,
                    dto.PollutantId,
                    dto.EmissionSourceId,
                    dto.ValidFrom,
                    dto.ValidTo
                ))
                .ToArray();

            var newPermit = await unitOfWork.PermitRepository.AddAsync(
                Permit.New(newPermitId, request.InstallationId, request.Number, request.PermitType, request.IssuedAt,
                    request.ValidUntil, request.Authority, request.Notes, emissionLimits), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return newPermit;
        }
        catch (Exception exception)
        {
            return new UnhandledPermitException(Guid.Empty, exception);
        }
    }
}