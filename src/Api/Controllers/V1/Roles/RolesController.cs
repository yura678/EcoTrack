using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Features.Role.Commands.AddRoleCommand;
using Application.Features.Role.Commands.DeleteRole;
using Application.Features.Role.Commands.UpdateRoleClaimsCommand;
using Application.Features.Role.Queries.GetAllRolesQuery;
using Application.Features.Role.Queries.GetAuthorizableRoutesQuery;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Roles;

[ApiVersion("1")]
[ApiController]
[Route("api/v{version:apiVersion}/roles")]
public class RolesController(ISender sender) : BaseController
{
    [Authorize(Roles = "superAdmin")]
    [HttpGet("all")]
    [ProducesOkApiResponseType<List<GetAllRolesQueryResponse>>]
    public async Task<IActionResult> ListAllRoles()
    {
        var result = await sender.Send(new GetAllRolesQuery());
        return Ok(result);
    }

    [Authorize(Roles = "admin")]
    [HttpGet("")]
    [ProducesOkApiResponseType<List<GetAllEnterpriseRolesQueryResponse>>]
    public async Task<IActionResult> ListEnterpriseRoles()
    {
        var result = await sender.Send(new GetAllEnterpriseRolesQuery());
        return Ok(result);
    }

    [Authorize(Roles = "superAdmin,admin")]
    [HttpGet("authorizable-routes")]
    [ProducesOkApiResponseType<List<GetAuthorizableRoutesQueryResponse>>]
    public async Task<IActionResult> GetAuthorizableRoutes()
    {
        var result = await sender.Send(new GetAuthorizableRoutesQuery());
        return Ok(result);
    }

    [Authorize(Roles = "superAdmin,admin")]
    [HttpPost("")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> AddRole(AddRoleCommand model)
    {
        var result = await sender.Send(model);
        return result.Match<ActionResult>(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [Authorize(Roles = "superAdmin,admin")]
    [HttpPut("{roleId:guid}/permissions")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> UpdatePermissions(
        [FromRoute] Guid roleId,
        [FromBody] List<string> permissions)
    {
        var result = await sender.Send(new UpdateRoleClaimsCommand(roleId, permissions));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }

    [Authorize(Roles = "superAdmin,admin")]
    [HttpDelete("{roleId:guid}")]
    [ProducesOkApiResponseType]
    public async Task<IActionResult> DeleteRole([FromRoute] Guid roleId)
    {
        var result = await sender.Send(new DeleteRoleCommand(roleId));
        return result.Match(
            response => Ok(response),
            error => error.ToObjectResult());
    }
}
