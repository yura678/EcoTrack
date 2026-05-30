using System.Globalization;
using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Models;
using Application.Features.Measurements.Command;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Monitoring;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/measurements")]
[ApiController]
public class MeasurementController(
    IMeasurementQueries measurementQueries,
    IRawMeasurementQueries rawMeasurementQueries,
    ISender sender) : BaseController
{
    [HttpGet]
    [ProducesOkApiResponseType<PageResult<MeasurementDto>>]
    public async Task<IActionResult> GetMeasurements(
        [FromQuery] MeasurementQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await measurementQueries.GetPagedAsync(
            query.InstallationId,
            query.EmissionSourceId,
            query.PollutantId,
            query.Window,
            query.Quality,
            query.From,
            query.To,
            query.Sort,
            query.Direction,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(new PageResult<MeasurementDto>
        {
            Items = result.Items.Select(MeasurementDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    /// <summary>
    /// KPI band data over the same filter set as <see cref="GetMeasurements"/>. Returns
    /// counts (total / valid / non-representative), per-quality breakdown, basic statistics
    /// (Min/Avg/Max), the latest WindowEnd, and — when pollutant is pinned — the unit symbol.
    /// </summary>
    [HttpGet("summary")]
    [ProducesOkApiResponseType<MeasurementSummaryDto>]
    public async Task<IActionResult> GetSummary(
        [FromQuery] MeasurementSummaryQueryDto query,
        CancellationToken cancellationToken)
    {
        var summary = await measurementQueries.GetSummaryAsync(
            query.InstallationId,
            query.EmissionSourceId,
            query.PollutantId,
            query.Window,
            query.Quality,
            query.From,
            query.To,
            cancellationToken);

        return Ok(MeasurementSummaryDto.FromReadModel(summary));
    }


    [HttpGet("{id:guid}")]
    [ProducesOkApiResponseType<MeasurementDto>]
    public async Task<IActionResult> GetMeasurement(
        [FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var entity = await measurementQueries.GetByIdAsync(id, cancellationToken);

        return entity.Match<ActionResult>(
            m => Ok(MeasurementDto.FromDomainModel(m)),
            () => NotFound());
    }

    [HttpPost]
    [ProducesOkApiResponseType<MeasurementDto>]
    public async Task<IActionResult> CreateMeasurement(
        [FromBody] CreateMeasurementDto request, CancellationToken cancellationToken)
    {
        var input = new CreateMeasurementCommand
        {
            Timestamp = request.Timestamp,
            EmissionSourceId = request.EmissionSourceId,
            PollutantId = request.PollutantId,
            DeviceId = request.DeviceId,
            UnitId = request.UnitId,
            Window = request.Window,
            Value = request.Value,
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            m => Ok(MeasurementDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }

    [HttpGet("timeseries")]
    [ProducesOkApiResponseType<IReadOnlyList<TimeSeriesPointDto>>]
    public async Task<IActionResult> GetTimeSeries(
        [FromQuery] TimeSeriesQueryDto query,
        CancellationToken cancellationToken)
    {
        var points = await rawMeasurementQueries.GetTimeSeriesAsync(
            query.PollutantId,
            query.EmissionSourceId,
            query.From,
            query.To,
            query.Window,
            query.Aggregation,
            cancellationToken);

        return Ok(points.Select(TimeSeriesPointDto.FromReadModel).ToList());
    }

    [HttpGet("heatmap")]
    [ProducesOkApiResponseType<IReadOnlyList<HeatmapPointDto>>]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] HeatmapQueryDto query,
        CancellationToken cancellationToken)
    {
        var points = await rawMeasurementQueries.GetHeatmapAsync(
            query.PollutantId,
            query.From,
            query.To,
            query.Aggregation,
            cancellationToken);

        return Ok(points.Select(HeatmapPointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Tenant-wide intensity heatmap with optional viewport (bbox) filter. The current
    /// tenant is resolved from the JWT — no enterprise id in the route — so the SPA can
    /// drive the map by panning/zooming and trim the result set via ST_Intersects on the
    /// GIST index. Existing per-installation /measurements/heatmap stays available.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/emissions-heatmap")]
    [ProducesOkApiResponseType<IReadOnlyList<HeatmapPointDto>>]
    public async Task<IActionResult> GetTenantHeatmap(
        [FromQuery] TenantHeatmapQueryDto query,
        CancellationToken cancellationToken)
    {
        BoundingBox? bbox = null;
        if (!string.IsNullOrWhiteSpace(query.Bbox))
        {
            bbox = ParseBbox(query.Bbox);
            if (bbox is null)
            {
                return Error(StatusCodes.Status400BadRequest,
                    "Expected 'minLng,minLat,maxLng,maxLat' with -180≤lng≤180 and -90≤lat≤90.",
                    "Bbox");
            }
        }

        var points = await rawMeasurementQueries.GetHeatmapAsync(
            query.PollutantId,
            query.From,
            query.To,
            query.Aggregation,
            cancellationToken,
            query.InstallationId,
            query.SiteId,
            bbox);

        return Ok(points.Select(HeatmapPointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Colour-scale ceiling for the intensity heatmap — the 98th percentile of per-source values
    /// across the requested scope (no bbox). The SPA uses this as a stable normalization
    /// denominator so map colours don't shift while panning/zooming. The scope filter matches
    /// the heatmap so the ceiling reflects exactly the visible set.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/emissions-heatmap/scale")]
    [ProducesOkApiResponseType<HeatmapScaleDto>]
    public async Task<IActionResult> GetTenantHeatmapScale(
        [FromQuery] HeatmapScaleQueryDto query,
        CancellationToken cancellationToken)
    {
        var scaleMax = await rawMeasurementQueries.GetHeatmapScaleAsync(
            query.PollutantId,
            query.From,
            query.To,
            query.Aggregation,
            cancellationToken,
            query.InstallationId,
            query.SiteId);

        return Ok(new HeatmapScaleDto(scaleMax));
    }

    private static BoundingBox? ParseBbox(string input)
    {
        var parts = input.Split(',');
        if (parts.Length != 4) return null;
        var ic = CultureInfo.InvariantCulture;
        if (!double.TryParse(parts[0], NumberStyles.Float, ic, out var minLng)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, ic, out var minLat)) return null;
        if (!double.TryParse(parts[2], NumberStyles.Float, ic, out var maxLng)) return null;
        if (!double.TryParse(parts[3], NumberStyles.Float, ic, out var maxLat)) return null;
        if (minLng >= maxLng || minLat >= maxLat) return null;
        if (minLng < -180 || maxLng > 180 || minLat < -90 || maxLat > 90) return null;
        return new BoundingBox(minLng, minLat, maxLng, maxLat);
    }

    /// <summary>
    /// Compliance-coloured heatmap: per source, returns severity (latest normalized value ÷
    /// active limit, both in base units) plus open-event count. Sources without an active
    /// rate-based limit on the pollutant are returned with severity=null so the UI can mark
    /// them as unmonitored instead of dropping them off the map.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/installations/{installationId:guid}/compliance-heatmap")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceHeatmapPointDto>>]
    public async Task<IActionResult> GetComplianceHeatmap(
        [FromRoute] Guid installationId,
        [FromQuery] Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var points = await measurementQueries.GetComplianceHeatmapAsync(
            installationId, pollutantId, cancellationToken);
        return Ok(points.Select(ComplianceHeatmapPointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Site-wide variant of the per-source compliance heatmap. One row per emission source of
    /// every installation belonging to the site, each carrying its InstallationId/Name so the
    /// UI can cluster sources by installation. Installation-level Concentration limits apply
    /// only to sources of their own installation, not across the whole site.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/sites/{siteId:guid}/compliance-heatmap")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceHeatmapPointDto>>]
    public async Task<IActionResult> GetSiteComplianceHeatmap(
        [FromRoute] Guid siteId,
        [FromQuery] Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var points = await measurementQueries.GetComplianceHeatmapBySiteAsync(
            siteId, pollutantId, cancellationToken);
        return Ok(points.Select(ComplianceHeatmapPointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Enterprise-wide variant of the per-source compliance heatmap — every emission source of the
    /// current tenant (resolved from the JWT, no id in the route). Same per-source severity shape;
    /// installation-level Concentration limits still apply only to their own installation's sources.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/compliance-heatmap")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceHeatmapPointDto>>]
    public async Task<IActionResult> GetEnterpriseComplianceHeatmap(
        [FromQuery] Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var points = await measurementQueries.GetComplianceHeatmapByEnterpriseAsync(
            pollutantId, cancellationToken);
        return Ok(points.Select(ComplianceHeatmapPointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Installation-level "Type II" aggregates: MassFlow and AnnualLoad limits where the
    /// regulator caps the sum across all sources of the installation. One row per active
    /// limit with the summed value, severity, and how many sources were excluded or
    /// derived to compute it.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/installations/{installationId:guid}/compliance-aggregates")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceAggregatePointDto>>]
    public async Task<IActionResult> GetComplianceAggregates(
        [FromRoute] Guid installationId,
        [FromQuery] Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var points = await measurementQueries.GetComplianceAggregatesAsync(
            installationId, pollutantId, cancellationToken);
        return Ok(points.Select(ComplianceAggregatePointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Site-wide variant of <see cref="GetComplianceAggregates"/>. Returns one row per
    /// installation-level MassFlow/AnnualLoad limit across every installation of the site.
    /// Sums are still scoped per-installation; each row carries InstallationId/Name so the UI
    /// can label or group the rows.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/sites/{siteId:guid}/compliance-aggregates")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceAggregatePointDto>>]
    public async Task<IActionResult> GetSiteComplianceAggregates(
        [FromRoute] Guid siteId,
        [FromQuery] Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var points = await measurementQueries.GetComplianceAggregatesBySiteAsync(
            siteId, pollutantId, cancellationToken);
        return Ok(points.Select(ComplianceAggregatePointDto.FromReadModel).ToList());
    }

    /// <summary>
    /// Enterprise-wide variant of <see cref="GetSiteComplianceAggregates"/> — one row per
    /// installation-level MassFlow/AnnualLoad limit across every installation of the current
    /// tenant. Sums stay scoped per-installation; each row carries its InstallationId/Name and a
    /// centroid so the UI can place one marker per installation.
    /// </summary>
    [HttpGet("/api/v{version:apiVersion}/compliance-aggregates")]
    [ProducesOkApiResponseType<IReadOnlyList<ComplianceAggregatePointDto>>]
    public async Task<IActionResult> GetEnterpriseComplianceAggregates(
        [FromQuery] Guid pollutantId,
        CancellationToken cancellationToken)
    {
        var points = await measurementQueries.GetComplianceAggregatesByEnterpriseAsync(
            pollutantId, cancellationToken);
        return Ok(points.Select(ComplianceAggregatePointDto.FromReadModel).ToList());
    }

    [HttpPut("{id:guid}/reject")]
    [ProducesOkApiResponseType<MeasurementDto>]
    public async Task<IActionResult> RejectMeasurement(
        [FromBody] RejectMeasurementDto request, [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var input = new RejectMeasurementCommand
        {
            Id = id,
            Reason = request.Reason,
        };
        var updatedEntity = await sender.Send(input, cancellationToken);

        return updatedEntity.Match(
            m => Ok(MeasurementDto.FromDomainModel(m)),
            e => e.ToObjectResult());
    }
}