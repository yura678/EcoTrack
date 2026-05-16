using Domain.Entities.Monitoring;

namespace Tests.Data.Monitoring;

public static class CalibrationRecordsData
{
    public static CalibrationRecord Passing(Guid deviceId)
        => CalibrationRecord.New(
            Guid.NewGuid(),
            deviceId,
            CalibrationType.Qal2,
            performedAt: DateTime.UtcNow.AddDays(-30),
            nextDueAt: DateTime.UtcNow.AddDays(60),
            result: CalibrationResult.Pass,
            performedBy: "Inspector",
            certificateNumber: "CERT-OK",
            notes: null);

    public static CalibrationRecord Failed(Guid deviceId)
        => CalibrationRecord.New(
            Guid.NewGuid(),
            deviceId,
            CalibrationType.Qal2,
            performedAt: DateTime.UtcNow.AddDays(-1),
            nextDueAt: DateTime.UtcNow.AddDays(89),
            result: CalibrationResult.Fail,
            performedBy: "Inspector",
            certificateNumber: "CERT-FAIL",
            notes: "Span check out of tolerance");

    public static CalibrationRecord Overdue(Guid deviceId)
        => CalibrationRecord.New(
            Guid.NewGuid(),
            deviceId,
            CalibrationType.Ast,
            performedAt: DateTime.UtcNow.AddDays(-400),
            nextDueAt: DateTime.UtcNow.AddDays(-30),
            result: CalibrationResult.Pass,
            performedBy: "Inspector",
            certificateNumber: "CERT-OLD",
            notes: null);
}
