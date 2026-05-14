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
        $"Required entity {missingEntityType.Name} with ID {missingEntityIdValue} was not found.")
{
    public Type MissingEntityType { get; } = missingEntityType;
    public Guid MissingEntityIdValue { get; } = missingEntityIdValue;
}

public class MeasurementNotFoundException(Guid measurementId)
    : MeasurementException(measurementId, $"Measurement with ID '{measurementId}' was not found.");

public class DuplicateMeasurementException(
    Guid id,
    Guid sourceId,
    Guid pollutantId,
    DateTime timestamp)
    : MeasurementException(
        id,
        $"A measurement for emission source {sourceId} with pollutant {pollutantId} already exists at timestamp {timestamp}.")
{
}

public class UnhandledMeasurementException(Guid id, Exception innerException)
    : MeasurementException(id,
        $"Unexpected error occurred.", innerException);
