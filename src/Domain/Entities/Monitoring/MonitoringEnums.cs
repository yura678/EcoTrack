namespace Domain.Entities.Monitoring;

public enum AveragingWindow
{
    Minute1 = 0,
    Minute10 = 1,
    HalfHour = 2,
    Hour1 = 3,
    Hour24 = 4,
    Month1 = 5,
    Year1 = 6
}

public enum Aggregation
{
    Average = 0,
    Max = 1,
    Min = 2,
    Sum = 3
}

public enum Quality
{
    Valid = 0,
    Calibration = 1,
    Maintenance = 2,
    Invalid = 3,
    Substituted = 4,
    Missing = 5
}

public enum SubstitutionSource
{
    Auto = 0,
    Manual = 1
}

public enum ParameterType
{
    StackTemperature = 0,
    StackPressure = 1,
    O2Content = 2,
    MoistureContent = 3,
    VolumetricFlow = 4,
    Velocity = 5
}

public enum LimitType
{
    Concentration = 0,
    MassFlow = 1,
    AnnualLoad = 2
}

public enum ComplianceEventType
{
    LimitExceedance = 0,
    DataAvailabilityLoss = 1,
    DeviceOffline = 2,
    CalibrationFailure = 3,
    MissingMeasurement = 4
}

public enum ComplianceEventStatus
{
    Open = 0,
    Investigating = 1,
    Closed = 2
}

public enum ResolutionReason
{
    SensorFault = 0,
    DataGap = 1,
    TrueExceedance = 2,
    OperatorAction = 3,
    Other = 4
}
