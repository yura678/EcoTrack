using System.ComponentModel.DataAnnotations;
using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Features.Admin.Commands.AddAdminCommand;
using Application.Features.Users.Commands.ChangeMemberRole;
using Application.Features.Users.Commands.ForcePasswordReset;
using Application.Features.Users.Commands.RevokeMembership;
using Application.Features.Users.Commands.SendInvitation;
using Application.Features.Users.Queries.GetUsers;
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

    public record ChangeMemberRoleBody(Guid RoleId);
}
