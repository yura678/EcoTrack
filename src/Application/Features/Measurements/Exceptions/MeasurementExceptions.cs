namespace Application.Features.Measurements.Exceptions;

public abstract class MeasurementException(
    Guid id,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid Id { get; } = id;
}

public class MeasurementRelatedEntityNotFoundException(
    Guid measurementId,
    Type missingEntityType,
    Guid missingEntityIdValue)
    : MeasurementException(measurementId,
        $"Related {ToFriendlyName(missingEntityType)} not found.")
{
    public Type MissingEntityType { get; } = missingEntityType;
    public Guid MissingEntityIdValue { get; } = missingEntityIdValue;

    private static string ToFriendlyName(Type t) => t.Name switch
    {
        "EmissionSource" => "emission source",
        "Pollutant" => "pollutant",
        "MeasureUnit" => "measurement unit",
        "MonitoringDevice" => "device",
        _ => t.Name.ToLowerInvariant()
    };
}

public class MeasurementNotFoundException(Guid measurementId)
    : MeasurementException(measurementId, "Measurement not found.");

public class DuplicateMeasurementException(
    Guid id,
    Guid sourceId,
    Guid pollutantId,
    DateTime timestamp)
    : MeasurementException(
        id,
        $"A measurement for this emission source and pollutant already exists at {timestamp:O}.")
{
    public Guid SourceId { get; } = sourceId;
    public Guid PollutantId { get; } = pollutantId;
    public DateTime Timestamp { get; } = timestamp;
}

public class UnhandledMeasurementException(Guid id, Exception innerException)
    : MeasurementException(id, "Unexpected error occurred.", innerException);
