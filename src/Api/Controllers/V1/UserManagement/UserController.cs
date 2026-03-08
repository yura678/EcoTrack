using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Api.Swagger;
using Application.Features.Admin.Commands.AddAdminCommand;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Commands.RefreshUserTokenCommand;
using Application.Features.Users.Commands.RegisterByInvitation;
using Application.Features.Users.Commands.RegisterEnterpriseAdmin;
using Application.Features.Users.Commands.RequestLogout;
using Application.Features.Users.Commands.SendInvitation;
using Application.Features.Users.Queries.GenerateUserToken;
using Application.Features.Users.Queries.TokenRequest;
using Application.Models.Jwt;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.UserManagement;

[ApiVersion("1")]
[ApiController]
[Route("api/v{version:apiVersion}/user")]
public class UserController : BaseController
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // [HttpPost("Register")]
    // [ProducesOkApiResponseType<UserCreateCommandResult>]
    // public async Task<IActionResult> CreateUser(UserCreateCommand model) // звичайний користувач не може зареэструватися
    // {
    //     var command = await _mediator.Send(model);
    //
    //     return command.Match(
    //         response => Ok(response),
    //         error => error.ToObjectResult()
    //     );
    // }

    [HttpPost("register-enterprise")]
    [ProducesOkApiResponseType<UserCreateCommandResult>]
    public async Task<IActionResult>
        RegisterEnterprise(RegisterEnterpriseAdminCommand model)
    {
        var command = await _mediator.Send(model);

        return command.Match(
            response => Ok(response),
            error => error.ToObjectResult()
        );
    }


    [HttpPost("register-by-invitation")]
    [ProducesOkApiResponseType<UserCreateCommandResult>]
    public async Task<IActionResult> RegisterByInvitation(RegisterByInvitationCommand model)
    {
        var command = await _mediator.Send(model);

        return command.Match(
            response => Ok(response),
            error => error.ToObjectResult()
        );
    }

    [HttpPost("token-request")]
    [ProducesOkApiResponseType<UserTokenRequestQueryResponse>]
    public async Task<IActionResult> TokenRequest(UserTokenRequestQuery model)
    {
        var query = await _mediator.Send(model);

        return query.Match(
            response => Ok(response),
            error => error.ToObjectResult()
        );
    }

    [HttpPost("login-confirmation")]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> ValidateUser(GenerateUserTokenQuery model)
    {
        var result = await _mediator.Send(model);

        return result.Match(
            token => Ok(token),
            error => error.ToObjectResult()
        );
    }

    [HttpPost("refresh-signIn")]
    [RequireTokenWithoutAuthorization]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> RefreshUserToken(RefreshUserTokenCommand model)
    {
        var checkCurrentAccessTokenValidity =
            await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

        if (checkCurrentAccessTokenValidity.Succeeded)
            return BadRequest("Current access token is valid. No need to refresh");

        var newTokenResult = await _mediator.Send(model);

        return newTokenResult.Match(
            token => Ok(token),
            error => error.ToObjectResult()
        );
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> RequestLogout()
    {
        var commandResult = await _mediator.Send(new RequestLogoutCommand(base.UserId));

        return commandResult.Match(
            result => Ok(result),
            error => error.ToObjectResult()
        );
    }

    [HttpPost("passwordToken-request")]
    [ProducesOkApiResponseType<AccessToken>]
    public async Task<IActionResult> PasswordTokenRequest(PasswordUserTokenRequestQuery model)
        => (await _mediator.Send(model)).Match(
            token => Ok(token),
            error => error.ToObjectResult()
        );
}