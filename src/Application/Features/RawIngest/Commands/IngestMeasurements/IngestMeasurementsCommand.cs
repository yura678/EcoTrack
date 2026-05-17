using Application.Features.RawIngest.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.RawIngest.Commands.IngestMeasurements;

public record IngestMeasurementInput(
    DateTime Time,
    Guid EmissionSourceId,
    Guid PollutantId,
    Guid UnitId,
    decimal RawValue,
    Quality Quality);

public record IngestMeasurementsResult(int InsertedCount);

public class IngestMeasurementsCommand
    : IRequest<Either<RawIngestException, IngestMeasurementsResult>>
{
    public required Guid DeviceId { get; init; }
    public required IReadOnlyList<IngestMeasurementInput> Batch { get; init; }
}
