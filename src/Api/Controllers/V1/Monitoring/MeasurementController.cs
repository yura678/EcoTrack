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
            Period = request.Period,
            Value = request.Value,
        };

        var newEntity = await sender.Send(input, cancellationToken);

        return newEntity.Match(
            m => Ok(MeasurementDto.FromDomainModel(m)),
            e => e.ToObjectResult());
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