using Application.Features.RawIngest.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.RawIngest.Commands.IngestProcessParameters;

public record IngestProcessParameterInput(
    DateTime Time,
    Guid EmissionSourceId,
    ParameterType ParameterType,
    decimal Value,
    Guid UnitId,
    Quality Quality);

public record IngestProcessParametersResult(int InsertedCount);

public class IngestProcessParametersCommand
    : IRequest<Either<RawIngestException, IngestProcessParametersResult>>
{
    public required Guid DeviceId { get; init; }
    public required IReadOnlyList<IngestProcessParameterInput> Batch { get; init; }
}
