using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Api.Modules.Errors;
using Application.Features.Profile.Commands.ChangeMyPassword;
using Application.Features.Profile.Commands.UpdateMyProfile;
using Application.Features.Profile.Queries.GetMyProfile;
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
}
