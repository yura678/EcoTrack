using Application.Common.Models;
using Domain.Entities.Monitoring;
using LanguageExt;

namespace Application.Common.Interfaces.Queries.Monitoring;

/// <summary>
/// One row of the compliance-oriented heatmap. Severity is computed server-side from the
/// latest Measurement (normalized when available) divided by the active EmissionLimit in
/// base units. Sources without an active limit on the requested pollutant return
/// Severity=null so the UI can mark them as unmonitored instead of dropping them.
/// </summary>
public record ComplianceHeatmapPoint(
    Guid EmissionSourceId,
    string EmissionSourceCode,
    double Latitude,
    double Longitude,
    Guid? LimitId,
    AveragingWindow? LimitPeriod,
    decimal? LimitValue,
    string? LimitUnitSymbol,
    decimal? CurrentValue,
    bool CurrentValueIsNormalized,
    decimal? Severity,
    int OpenEventCount,
    DateTime? MeasuredAt);

public interface IMeasurementQueries
{
    Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PageResult<Measurement>> GetPagedAsync(Guid installationId, DateTime? from,
        DateTime? to, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Per-source view ready for compliance-coloring: pulls the latest Measurement for the
    /// shortest-period active limit on (source, pollutantId) and divides its normalized value
    /// by the limit, both reduced to base units. Sources without an active limit are still
    /// returned but with Severity / LimitValue / CurrentValue = null.
    /// </summary>
    Task<IReadOnlyList<ComplianceHeatmapPoint>> GetComplianceHeatmapAsync(
        Guid installationId, Guid pollutantId, CancellationToken cancellationToken);
}