using Application.Common.Interfaces.Ingestion;
using Application.Features.RawIngest.Exceptions;
using Domain.Entities.Monitoring;
using LanguageExt;
using MediatR;

namespace Application.Features.RawIngest.Commands.IngestProcessParameters;

public class IngestProcessParametersCommandHandler(
    IRawProcessParameterWriter writer)
    : IRequestHandler<IngestProcessParametersCommand,
        Either<RawIngestException, IngestProcessParametersResult>>
{
    public async Task<Either<RawIngestException, IngestProcessParametersResult>> Handle(
        IngestProcessParametersCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entities = request.Batch.Select(b => RawProcessParameter.New(
                b.Time.ToUniversalTime(), b.EmissionSourceId, request.DeviceId,
                b.ParameterType, b.Value, b.UnitId, b.Quality));
            var inserted = await writer.WriteBatchAsync(entities, cancellationToken);
            return new IngestProcessParametersResult(inserted);
        }
        catch (Exception ex)
        {
            return new UnhandledRawIngestException(ex);
        }
    }
}
