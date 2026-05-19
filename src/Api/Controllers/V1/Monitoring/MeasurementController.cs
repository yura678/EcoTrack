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
        var result = await measurementQueries.GetPagedAsync(query.InstallationId,
            query.From, query.To, query.Page, query.PageSize, cancellationToken);

        return Ok(new PageResult<MeasurementDto>
        {
            Items = result.Items.Select(MeasurementDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
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