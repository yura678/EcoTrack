using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Common.Models;
using Application.Features.Profile.Commands.ChangeMyPassword;
using Application.Features.Profile.Commands.RevokeAllMySessions;
using Application.Features.Profile.Commands.RevokeMySession;
using Application.Features.Profile.Commands.UpdateMyProfile;
using Application.Features.Profile.Queries.GetMyLoginHistory;
using Application.Features.Profile.Queries.GetMyProfile;
using Application.Features.Profile.Queries.GetMySessions;
using Application.Models.Profile;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Users;

/// <summary>
/// Self-service profile management. Every endpoint targets the caller (resolved from JWT
/// nameidentifier claim) — there is no userId in any of the routes. Admin views of other
/// users go through UsersController instead.
/// </summary>
[ApiVersion("1")]
[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/profile")]
public class ProfileController(ISender sender) : BaseController
{
    [HttpGet("me")]
    [ProducesOkApiResponseType<MyProfileInfo>]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyProfileQuery(), cancellationToken);
        return result.Match(
            profile => Ok(profile),
            error => error.ToObjectResult());
    }

    [HttpPatch("me")]
    [ProducesOkApiResponseType<MyProfileInfo>]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileDto body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMyProfileCommand
        {
            Name = body.Name,
            FamilyName = body.FamilyName
        };
        var result = await sender.Send(command, cancellationToken);
        return result.Match(
            profile => Ok(profile),
            error => error.ToObjectResult());
    }

    [HttpPost("change-password")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ChangeMyPassword(
        [FromBody] ChangeMyPasswordDto body,
        CancellationToken cancellationToken)
    {
        var command = new ChangeMyPasswordCommand
        {
            CurrentPassword = body.CurrentPassword,
            NewPassword = body.NewPassword
        };
        var result = await sender.Send(command, cancellationToken);
        return result.Match<IActionResult>(
            _ => Ok(),
            error => error.ToObjectResult());
    }

    [HttpGet("sessions")]
    [ProducesOkApiResponseType<IReadOnlyList<SessionInfo>>]
    public async Task<IActionResult> GetMySessions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMySessionsQuery(), cancellationToken);
        return result.Match(
            sessions => Ok(sessions),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Revoke a single session. Idempotent — 200 even if the session was already invalid /
    /// belonged to someone else, the post-condition "this session is not live" holds either way.
    /// </summary>
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> RevokeMySession(
        [FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevokeMySessionCommand(sessionId), cancellationToken);
        return result.Match<IActionResult>(
            _ => Ok(),
            error => error.ToObjectResult());
    }

    /// <summary>"Log out everywhere" — flips IsValid=false on every active token of the caller.</summary>
    [HttpDelete("sessions")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> RevokeAllMySessions(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevokeAllMySessionsCommand(), cancellationToken);
        return result.Match<IActionResult>(
            _ => Ok(),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Paged login history for the caller — both successes and failures, newest first.
    /// Optional from/to bounds; defaults to the most recent <paramref name="pageSize"/> rows.
    /// </summary>
    [HttpGet("login-history")]
    [ProducesOkApiResponseType<PageResult<LoginHistoryEntry>>]
    public async Task<IActionResult> GetMyLoginHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetMyLoginHistoryQuery(from, to, page, pageSize), cancellationToken);
        return result.Match(
            history => Ok(history),
            error => error.ToObjectResult());
    }
}
