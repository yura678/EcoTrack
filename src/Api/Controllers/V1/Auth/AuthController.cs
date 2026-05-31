using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules;
using Api.Modules.Errors;
using Api.Swagger;
using Application.Features.Auth.Commands.ConfirmEmail;
using Application.Features.Auth.Commands.RequestPasswordReset;
using Application.Features.Auth.Commands.ResendEmailConfirmation;
using Application.Features.Auth.Commands.ResetPassword;
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
using Application.Common.Settings;
using Application.Models.Auth;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers.V1.Auth;

[ApiVersion("1")]
[ApiController]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(
    ISender sender,
    RefreshCookieSettings refreshCookie,
    IAntiforgery antiforgery) : BaseController
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

    // ---- Password reset (self-service) ----

    /// <summary>
    /// Step 1 of self-service password reset. Always returns 200 regardless of whether the
    /// email is known — anti-enumeration. If the address exists, an email with a single-use
    /// reset link is sent.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ForgotPassword(RequestPasswordResetCommand model)
    {
        await sender.Send(model);
        return Ok();
    }

    /// <summary>
    /// Step 2 of self-service password reset. Consumes the token, sets the new password,
    /// invalidates all of the user's refresh tokens.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand model)
    {
        var result = await sender.Send(model);
        return result.Match<IActionResult>(
            _ => Ok(),
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
    [ProducesOkApiResponseType<AuthSession>]
    public async Task<IActionResult> LoginByCode(LoginByCodeQuery model)
    {
        var result = await sender.Send(model);
        return result.Match(
            OkWithRefreshCookie,
            error => error.ToObjectResult());
    }

    // ---- Login by password ----

    [HttpPost("login/password")]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType<AuthSession>]
    public async Task<IActionResult> LoginByPassword(LoginByPasswordQuery model)
    {
        var result = await sender.Send(model);
        return result.Match(
            OkWithRefreshCookie,
            error => error.ToObjectResult());
    }

    // ---- Session management ----

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [RequireTokenWithoutAuthorization]
    [ProducesOkApiResponseType<AuthSession>]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        // The refresh token rides in an httpOnly cookie, so this endpoint is cookie-authenticated and
        // must validate the antiforgery token (X-XSRF-TOKEN header vs the antiforgery cookie).
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Error(StatusCodes.Status403Forbidden, "Invalid or missing antiforgery token.");
        }

        if (!Request.Cookies.TryGetValue(refreshCookie.Name, out var raw)
            || !Guid.TryParse(raw, out var refreshTokenId)
            || refreshTokenId == Guid.Empty)
        {
            return Error(StatusCodes.Status401Unauthorized, "Missing or invalid refresh token.");
        }

        var currentTokenValid = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (currentTokenValid.Succeeded)
            return Error(StatusCodes.Status400BadRequest, "Current access token is valid. No need to refresh");

        var result = await sender.Send(new RefreshUserTokenCommand(refreshTokenId), cancellationToken);
        return result.Match(
            OkWithRefreshCookie,
            error => error.ToObjectResult());
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> Logout()
    {
        // Drop the refresh cookie client-side; the command invalidates every stored refresh token
        // server-side. Logout is Bearer-authenticated (not cookie-authenticated) so it needs no
        // antiforgery check.
        Response.ClearRefreshCookie(refreshCookie);
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
    [ProducesOkApiResponseType<AuthSession>]
    public async Task<IActionResult> SwitchEnterprise(
        [FromRoute] Guid enterpriseId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SwitchEnterpriseCommand { EnterpriseId = enterpriseId },
            cancellationToken);
        return result.Match(
            OkWithRefreshCookie,
            e => e.ToObjectResult());
    }

    // ---- CSRF + refresh-cookie helpers ----

    /// <summary>
    /// Seeds the antiforgery token pair: the secret in an httpOnly cookie (set by GetAndStoreTokens)
    /// plus a JS-readable XSRF-TOKEN cookie carrying the request token, which the SPA echoes back in
    /// the X-XSRF-TOKEN header. The SPA calls this once on cold start (before its first /refresh) and
    /// it is also refreshed implicitly on every token-issuing response via <see cref="OkWithRefreshCookie"/>.
    /// </summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesOkApiResponseType]
    public IActionResult Csrf()
    {
        SeedAntiforgery();
        return Ok();
    }

    private IActionResult OkWithRefreshCookie(AuthSession session)
    {
        SeedAntiforgery();
        Response.AppendRefreshCookie(session.Token.refresh_token, refreshCookie);
        // Never expose the refresh token in the JSON body — it lives only in the httpOnly cookie now.
        session.Token.refresh_token = string.Empty;
        return Ok(session);
    }

    private void SeedAntiforgery()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // readable by the SPA so it can echo it in the X-XSRF-TOKEN header
            Secure = refreshCookie.Secure,
            SameSite = RefreshCookieExtensions.ParseSameSite(refreshCookie.SameSite),
            Path = "/",
            IsEssential = true,
        });
    }
}
