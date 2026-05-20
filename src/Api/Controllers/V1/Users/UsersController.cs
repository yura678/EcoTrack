using System.ComponentModel.DataAnnotations;
using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Common.Models;
using Application.Features.Admin.Commands.AddAdminCommand;
using Application.Features.Users.Commands.ChangeMemberRole;
using Application.Features.Users.Commands.ChangeUserEmail;
using Application.Features.Users.Commands.ForcePasswordReset;
using Application.Features.Users.Commands.RevokeMembership;
using Application.Features.Users.Commands.RevokeUserSessions;
using Application.Features.Users.Commands.SendInvitation;
using Application.Features.Users.Commands.UnlockUser;
using Application.Features.Users.Queries.GetUserDetail;
using Application.Features.Users.Queries.GetUserLoginHistory;
using Application.Features.Users.Queries.GetUsers;
using Application.Models.Profile;
using Application.Models.Users;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Users;

[ApiVersion("1")]
[ApiController]
[Route("api/v{version:apiVersion}/users")]
[Display(Description = "Enterprise users management")]
public class UsersController(ISender sender) : BaseController
{
    [Authorize(Roles = "admin")]
    [HttpGet("")]
    [ProducesOkApiResponseType<List<GetUsersQueryResponse>>]
    public async Task<IActionResult> ListEnterpriseUsers()
    {
        var result = await sender.Send(new GetEnterpriseUsersQuery());
        return Ok(result);
    }

    [Authorize(Roles = "superAdmin")]
    [HttpGet("all")]
    [ProducesOkApiResponseType<List<GetUsersQueryResponse>>]
    public async Task<IActionResult> ListAllUsers()
    {
        var result = await sender.Send(new GetUsersQuery());
        return Ok(result);
    }

    [Authorize(Roles = "admin,superAdmin")]
    [HttpPost("invite")]
    [ProducesOkApiResponseType<string>]
    public async Task<IActionResult> Invite(SendInvitationCommand model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [Authorize(Roles = "superAdmin,admin")]
    [HttpPost("")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> AddUser(AddAdminCommand model)
    {
        var result = await sender.Send(model);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [Authorize(Roles = "admin,superAdmin")]
    [HttpPut("{userId:guid}/role")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ChangeMemberRole(
        [FromRoute] Guid userId,
        [FromBody] ChangeMemberRoleBody body)
    {
        var result = await sender.Send(new ChangeMemberRoleCommand(userId, body.RoleId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [Authorize(Roles = "admin,superAdmin")]
    [HttpDelete("{userId:guid}/membership")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> RevokeMembership([FromRoute] Guid userId)
    {
        var result = await sender.Send(new RevokeMembershipCommand(userId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Admin force-password-reset. Generates an Identity reset token and emails the link to
    /// the user. No password change happens server-side here — the user must click the link
    /// and submit a new password via /api/v1/auth/reset-password. Action is audited
    /// immediately as <see cref="Domain.Entities.Auditing.AuditAction.UserPasswordResetRequested"/>.
    /// </summary>
    [Authorize(Roles = "admin,superAdmin")]
    [HttpPost("{userId:guid}/force-password-reset")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ForcePasswordReset([FromRoute] Guid userId)
    {
        var result = await sender.Send(new ForcePasswordResetCommand(userId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Admin user-detail page payload: identity + membership in caller's enterprise + lockout
    /// state. Returns 404 for users without an active membership in this enterprise (admin
    /// must not enumerate cross-tenant users).
    /// </summary>
    [Authorize(Roles = "admin,superAdmin")]
    [HttpGet("{userId:guid}")]
    [ProducesOkApiResponseType<UserDetailInfo>]
    public async Task<IActionResult> GetUserDetail([FromRoute] Guid userId)
    {
        var result = await sender.Send(new GetUserDetailQuery(userId));
        return result.Match(
            detail => Ok(detail),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Admin unlock — clears LockoutEnd + AccessFailedCount. Idempotent; audited as
    /// <see cref="Domain.Entities.Auditing.AuditAction.UserAccountUnlocked"/>.
    /// </summary>
    [Authorize(Roles = "admin,superAdmin")]
    [HttpPost("{userId:guid}/unlock")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> UnlockUser([FromRoute] Guid userId)
    {
        var result = await sender.Send(new UnlockUserCommand(userId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Admin "log out everywhere in my tenant" — kills the target user's refresh tokens for
    /// the admin's enterprise only. Sessions in other tenants stay live. Audited as
    /// <see cref="Domain.Entities.Auditing.AuditAction.UserSessionsRevoked"/>.
    /// </summary>
    [Authorize(Roles = "admin,superAdmin")]
    [HttpDelete("{userId:guid}/sessions")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> RevokeUserSessions([FromRoute] Guid userId)
    {
        var result = await sender.Send(new RevokeUserSessionsCommand(userId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Admin-initiated email change. Replaces user's Email + UserName, invalidates current-tenant
    /// refresh tokens, audits as <see cref="Domain.Entities.Auditing.AuditAction.UserEmailChanged"/>.
    /// No OTP — the admin is trusted to verify the new address out-of-band.
    /// </summary>
    [Authorize(Roles = "admin,superAdmin")]
    [HttpPut("{userId:guid}/email")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> ChangeUserEmail(
        [FromRoute] Guid userId,
        [FromBody] ChangeUserEmailBody body)
    {
        var command = new ChangeUserEmailCommand { UserId = userId, NewEmail = body.NewEmail };
        var result = await sender.Send(command);
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    /// <summary>
    /// Admin view of a user's login history. Same tenant scoping as user-detail — admin only
    /// sees history of users in their enterprise; 404 otherwise.
    /// </summary>
    [Authorize(Roles = "admin,superAdmin")]
    [HttpGet("{userId:guid}/login-history")]
    [ProducesOkApiResponseType<PageResult<LoginHistoryEntry>>]
    public async Task<IActionResult> GetUserLoginHistory(
        [FromRoute] Guid userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await sender.Send(
            new GetUserLoginHistoryQuery(userId, from, to, page, pageSize));
        return result.Match(
            history => Ok(history),
            error => error.ToObjectResult());
    }

    public record ChangeMemberRoleBody(Guid RoleId);
    public record ChangeUserEmailBody(string NewEmail);
}
