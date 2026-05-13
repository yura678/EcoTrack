using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Features.Auth.Commands.SwitchEnterprise;
using Application.Features.Auth.Queries.GetMemberships;
using Application.Models.Auth;
using Application.Models.Jwt;
using Asp.Versioning;
using Infrastructure.Identity.PermissionManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Auth;

[ApiVersion("1")]
[Authorize(ConstantPolicies.DynamicPermission)]
[Route("api/v{version:apiVersion}/auth")]
[ApiController]
public class AuthController(ISender sender) : BaseController
{
    [HttpGet("memberships")]
    [ProducesOkApiResponseType<IReadOnlyList<MembershipInfo>>]
    public async Task<IActionResult> GetMemberships(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMembershipsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("switch-enterprise/{enterpriseId:guid}")]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> SwitchEnterprise(
        [FromRoute] Guid enterpriseId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SwitchEnterpriseCommand { EnterpriseId = enterpriseId },
            cancellationToken);
        return result.Match(
            token => Ok(token),
            e => e.ToObjectResult());
    }
}
