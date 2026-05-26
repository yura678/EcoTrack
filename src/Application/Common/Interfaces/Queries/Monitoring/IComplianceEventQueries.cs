using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

public interface IComplianceEventQueries
{
    Task<IReadOnlyList<ComplianceEvent>> GetByMeasurementIdAsync(Guid measurementId,
        CancellationToken cancellationToken);

    /// <param name="enterpriseId">When non-null, scope the result to a single tenant. The per-tenant
    /// Hangfire detection fan-out passes its own enterprise so it doesn't fetch every other
    /// tenant's open events on each 5-min tick.</param>
    Task<IReadOnlyList<ComplianceEvent>> GetOpenByTypeAsync(ComplianceEventType eventType,
        CancellationToken cancellationToken, Guid? enterpriseId = null);

    /// <summary>
    /// Returns the event with <see cref="ComplianceEvent.EmissionSource"/> and
    /// <see cref="ComplianceEvent.Device"/> loaded — enough to build the lean
    /// list/broadcast DTOs without leaking UUIDs to the UI.
    /// </summary>
    Task<Option<ComplianceEvent>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the event with the full graph required by the details view: source + site +
    /// installation, measurement + pollutant + unit, limit + pollutant + unit, device, and the
    /// resolving user's display fields (denormalised so we don't take an Identity dep here).
    /// </summary>
    Task<Option<ComplianceEventDetailView>> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<ComplianceEvent>> GetPagedAsync(
        ComplianceEventStatus? status,
        ComplianceEventType? eventType,
        Guid? emissionSourceId,
        Guid? deviceId,
        Guid? installationId,
        Guid? siteId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record ComplianceEventDetailView(
    ComplianceEvent Event,
    string? ResolvedByName,
    string? ResolvedByFamilyName,
    string? ResolvedByEmail);
