using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Queries.Monitoring;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Monitoring;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/process-parameters")]
[ApiController]
public class ProcessParameterController(IRawProcessParameterQueries queries) : BaseController
{
    [HttpGet("timeseries")]
    [ProducesOkApiResponseType<IReadOnlyList<ProcessParameterPointDto>>]
    public async Task<IActionResult> GetTimeSeries(
        [FromQuery] ProcessParameterTimeSeriesQueryDto query,
        CancellationToken cancellationToken)
    {
        var points = await queries.GetTimeSeriesAsync(
            query.EmissionSourceId, query.ParameterType,
            query.From, query.To, query.Window,
            cancellationToken);

        return Ok(points.Select(ProcessParameterPointDto.FromReadModel).ToList());
    }

    [HttpGet("latest")]
    [ProducesOkApiResponseType<IReadOnlyList<ProcessParameterLatestDto>>]
    public async Task<IActionResult> GetLatest(
        [FromQuery] ProcessParameterLatestQueryDto query,
        CancellationToken cancellationToken)
    {
        var latest = await queries.GetLatestForSourceAsync(
            query.EmissionSourceId, query.ParameterTypes,
            cancellationToken);

        return Ok(latest.Select(ProcessParameterLatestDto.FromReadModel).ToList());
    }
}
