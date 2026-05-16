using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Api.Swagger;
using Application.Features.Auth.Commands.ConfirmEmail;
using Application.Features.Auth.Commands.ResendEmailConfirmation;
using Application.Features.Auth.Commands.SwitchEnterprise;
using Application.Features.Auth.Queries.GetMemberships;
using Application.Features.Auth.Queries.LoginByCode;
using Application.Features.Auth.Queries.LoginByPassword;
using Application.Features.Auth.Queries.RequestLoginCode;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Commands.RefreshUserTokenCommand;
using Application.Features.Users.Commands.RegisterByInvitation;
using Application.Features.Users.Commands.RegisterEnterpriseAdmin;
using Application.Features.Users.Commands.RequestLogout;
using Application.Models.Auth;
using Application.Models.Jwt;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers.V1.Auth;

[ApiVersion("1")]
[ApiController]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(ISender sender) : BaseController
{
    // ---- Registration ----

    [HttpPost("register-enterprise")]
    [ProducesOkApiResponseType<UserCreateCommandResult>]
    public async Task<IActionResult> RegisterEnterprise(RegisterEnterpriseAdminCommand model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [HttpPost("register-by-invitation")]
    [ProducesOkApiResponseType<UserCreateCommandResult>]
    public async Task<IActionResult> RegisterByInvitation(RegisterByInvitationCommand model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    // ---- Email confirmation ----

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailCommand model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ResendConfirmation(ResendEmailConfirmationCommand model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    // ---- Login by email code ----

    [HttpPost("login/request-code")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> RequestLoginCode(RequestLoginCodeQuery model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [HttpPost("login/verify-code")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> LoginByCode(LoginByCodeQuery model)
    {
        var result = await sender.Send(model);
        return result.Match(
            token => Ok(token),
            error => error.ToObjectResult());
    }

    // ---- Login by password ----

    [HttpPost("login/password")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> LoginByPassword(LoginByPasswordQuery model)
    {
        var result = await sender.Send(model);
        return result.Match(
            token => Ok(token),
            error => error.ToObjectResult());
    }

    // ---- Session management ----

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [RequireTokenWithoutAuthorization]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> Refresh(RefreshUserTokenCommand model)
    {
        var currentTokenValid = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (currentTokenValid.Succeeded)
            return BadRequest("Current access token is valid. No need to refresh");

        var result = await sender.Send(model);
        return result.Match(
            token => Ok(token),
            error => error.ToObjectResult());
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> Logout()
    {
        var result = await sender.Send(new RequestLogoutCommand(UserId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    // ---- Active enterprise context ----

    [HttpGet("memberships")]
    [Authorize]
    [ProducesOkApiResponseType<IReadOnlyList<MembershipInfo>>]
    public async Task<IActionResult> GetMemberships(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMembershipsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("switch-enterprise/{enterpriseId:guid}")]
    [Authorize]
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
