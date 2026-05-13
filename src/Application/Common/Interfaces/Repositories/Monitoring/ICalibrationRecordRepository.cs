using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories.Monitoring;

public interface ICalibrationRecordRepository
{
    Task<Option<CalibrationRecord>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CalibrationRecord> AddAsync(CalibrationRecord entity, CancellationToken cancellationToken);
    CalibrationRecord Update(CalibrationRecord entity);
    CalibrationRecord Delete(CalibrationRecord entity);
}
