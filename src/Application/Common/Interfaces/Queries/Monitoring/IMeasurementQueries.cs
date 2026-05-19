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
    Guid InstallationId,
    string InstallationName,
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
    Guid InstallationId,
    string InstallationName,
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
    /// Same shape as <see cref="GetComplianceHeatmapAsync"/> but scoped to a Site — returns every
    /// emission source of every installation belonging to the site. Per-installation limits
    /// (Concentration installation-wide) only apply to their own installation's sources, not
    /// across the whole site.
    /// </summary>
    Task<IReadOnlyList<ComplianceHeatmapPoint>> GetComplianceHeatmapBySiteAsync(
        Guid siteId, Guid pollutantId, CancellationToken cancellationToken);

    /// <summary>
    /// Installation-level "Type II" limits (MassFlow + AnnualLoad) summed across the
    /// installation's sources. Mirrors the detector's ProcessInstallationAggregates /
    /// ProcessAnnualLoadAggregates logic — including derived mass flow when a source reports
    /// concentration + volumetric flow instead of mass flow directly, and O₂ normalization
    /// for AnnualLoad Concentration limits.
    /// </summary>
    Task<IReadOnlyList<ComplianceAggregatePoint>> GetComplianceAggregatesAsync(
        Guid installationId, Guid pollutantId, CancellationToken cancellationToken);

    /// <summary>
    /// Site-wide variant of <see cref="GetComplianceAggregatesAsync"/> — returns one row per
    /// installation-level MassFlow/AnnualLoad limit across every installation that belongs to
    /// the site. Each row carries InstallationId/Name so the UI can group rows by installation.
    /// Sums are still scoped per-installation (a limit on installation A only sums sources of
    /// installation A).
    /// </summary>
    Task<IReadOnlyList<ComplianceAggregatePoint>> GetComplianceAggregatesBySiteAsync(
        Guid siteId, Guid pollutantId, CancellationToken cancellationToken);
}