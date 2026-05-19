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

/// <summary>
/// One installation-level "Type II" limit (MassFlow / AnnualLoad) summed across the
/// installation's sources. AggregateValue is in the limit's unit; Severity is the same
/// number divided by LimitValue. ExcludedSourcesCount reports how many sources fell out
/// of the sum (missing measurement, incompatible dimension, missing flow for derivation).
/// </summary>
public record ComplianceAggregatePoint(
    Guid LimitId,
    LimitType LimitType,
    AveragingWindow LimitPeriod,
    decimal LimitValue,
    string LimitUnitSymbol,
    decimal? AggregateValue,
    decimal? Severity,
    int ContributingSourcesCount,
    int DerivedSourcesCount,
    int ExcludedSourcesCount,
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

    /// <summary>
    /// Installation-level "Type II" limits (MassFlow + AnnualLoad) summed across the
    /// installation's sources. Mirrors the detector's ProcessInstallationAggregates /
    /// ProcessAnnualLoadAggregates logic — including derived mass flow when a source reports
    /// concentration + volumetric flow instead of mass flow directly, and O₂ normalization
    /// for AnnualLoad Concentration limits.
    /// </summary>
    Task<IReadOnlyList<ComplianceAggregatePoint>> GetComplianceAggregatesAsync(
        Guid installationId, Guid pollutantId, CancellationToken cancellationToken);
}