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
[Route("api/v{version:apiVersion}/compliance-audit")]
[ApiController]
public class ComplianceAuditController(IRawMeasurementQueries queries) : BaseController
{
    [HttpGet("simulate")]
    [ProducesOkApiResponseType<ComplianceAuditResultDto>]
    public async Task<IActionResult> Simulate(
        [FromQuery] ComplianceAuditQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetComplianceAuditAsync(
            new ComplianceAuditQueryParams(
                query.EmissionSourceId, query.PollutantId,
                query.LimitValue, query.LimitUnitId, query.Period,
                query.From, query.To),
            cancellationToken);

        if (result is null)
        {
            return BadRequest(new { error = "Limit unit not found or period not supported." });
        }

        return Ok(ComplianceAuditResultDto.FromReadModel(result));
    }
}
