using Domain.Entities.Monitoring;

namespace Application.Common.Interfaces.Queries.Monitoring;

/// <summary>
/// Tells whether a previously detected ComplianceEvent is still describing a live violation
/// right now (e.g. the limit is still being exceeded; the device is still offline). An event
/// stays Open until an operator closes it, so this is the only way to distinguish ongoing
/// emergencies from historical incidents pending review.
/// </summary>
public interface ICurrentViolationProbe
{
    /// <summary>
    /// Returns a per-event answer keyed by ComplianceEvent.Id:
    ///   true  → violation is still happening right now,
    ///   false → condition has resolved,
    ///   null  → indeterminate (event type not probed, missing reference data, etc.).
    /// Events not present in the result must be treated as null by the caller.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool?>> ProbeAsync(
        IReadOnlyCollection<ComplianceEvent> events,
        CancellationToken cancellationToken);
}
