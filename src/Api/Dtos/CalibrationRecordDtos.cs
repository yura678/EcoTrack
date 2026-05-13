using Domain.Entities.Monitoring;

namespace Api.Dtos;

public record CreateCalibrationRecordDto(
    CalibrationType Type,
    DateTime PerformedAt,
    DateTime NextDueAt,
    CalibrationResult Result,
    string PerformedBy,
    string? CertificateNumber,
    string? Notes);

public record UpdateCalibrationRecordDto(
    CalibrationType Type,
    DateTime PerformedAt,
    DateTime NextDueAt,
    CalibrationResult Result,
    string PerformedBy,
    string? CertificateNumber,
    string? Notes);

public record CalibrationRecordDto(
    Guid Id,
    Guid DeviceId,
    CalibrationType Type,
    DateTime PerformedAt,
    DateTime NextDueAt,
    CalibrationResult Result,
    string PerformedBy,
    string? CertificateNumber,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static CalibrationRecordDto FromDomainModel(CalibrationRecord record)
    {
        return new CalibrationRecordDto(
            record.Id,
            record.DeviceId,
            record.Type,
            record.PerformedAt,
            record.NextDueAt,
            record.Result,
            record.PerformedBy,
            record.CertificateNumber,
            record.Notes,
            record.CreatedAt,
            record.UpdatedAt
        );
    }
}
