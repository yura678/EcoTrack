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
    : CalibrationRecordException(id, "Calibration record not found.");

public class CalibrationDeviceNotFoundException(Guid deviceId)
    : CalibrationRecordException(Guid.Empty, "Device not found.")
{
    public Guid DeviceId { get; } = deviceId;
}

public class CalibrationInvalidScheduleException(DateTime performedAt, DateTime nextDueAt)
    : CalibrationRecordException(Guid.Empty,
        "Next calibration date must be after the calibration date.")
{
    public DateTime PerformedAt { get; } = performedAt;
    public DateTime NextDueAt { get; } = nextDueAt;
}

public class UnhandledCalibrationRecordException(Guid id, Exception? innerException = null)
    : CalibrationRecordException(id, "Unexpected error occurred.", innerException);
