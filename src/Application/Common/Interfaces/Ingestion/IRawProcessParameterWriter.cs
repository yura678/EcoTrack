using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Ingestion;

public interface IRawProcessParameterWriter
{
    Task<int> WriteBatchAsync(IEnumerable<RawProcessParameter> parameters, CancellationToken cancellationToken);
}
