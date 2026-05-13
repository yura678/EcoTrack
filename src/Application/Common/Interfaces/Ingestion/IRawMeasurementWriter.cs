using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Ingestion;

public interface IRawMeasurementWriter
{
    Task<int> WriteBatchAsync(IEnumerable<RawMeasurement> measurements, CancellationToken cancellationToken);
}
