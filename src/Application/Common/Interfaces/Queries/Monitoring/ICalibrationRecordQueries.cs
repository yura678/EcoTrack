using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface ICalibrationRecordQueries
{
    Task<IReadOnlyList<CalibrationRecord>> GetByDeviceIdAsync(Guid deviceId,
        CancellationToken cancellationToken);
    Task<Option<CalibrationRecord>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
