namespace Application.Features.CalibrationRecords.Exceptions;

public abstract class CalibrationRecordException(
    Guid id,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid Id { get; } = id;
}

public class CalibrationRecordNotFoundException(Guid id)
    : CalibrationRecordException(id, $"Calibration record with ID '{id}' was not found.");

public class CalibrationDeviceNotFoundException(Guid deviceId)
    : CalibrationRecordException(Guid.Empty,
        $"Device with ID '{deviceId}' was not found.");

public class CalibrationInvalidScheduleException(DateTime performedAt, DateTime nextDueAt)
    : CalibrationRecordException(Guid.Empty,
        $"NextDueAt ({nextDueAt:O}) must be after PerformedAt ({performedAt:O}).");

public class UnhandledCalibrationRecordException(Guid id, Exception? innerException = null)
    : CalibrationRecordException(id, "Unexpected error occurred.", innerException);
