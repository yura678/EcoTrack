using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Features.Role.Commands.AddRoleCommand;
using Application.Features.Role.Commands.UpdateRoleClaimsCommand;
using Application.Features.Role.Queries.GetAllRolesQuery;
using Application.Features.Role.Queries.GetAuthorizableRoutesQuery;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Admin
{
    [ApiVersion("1")]
    [ApiController]
    [Route("api/v{version:apiVersion}/role-manager")]
    public class RoleManagerController(ISender sender) : BaseController
    {
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("roles")]
        [ProducesOkApiResponseType<List<GetAllRolesQueryResponse>>]
        public async Task<IActionResult> GetRoles()
        {
            var queryResult = await sender.Send(new GetAllRolesQuery());
            return Ok(queryResult);
        }
        
        [Authorize(Roles = "admin")]
        [HttpGet("enterprise-roles")]
        [ProducesOkApiResponseType<List<GetAllEnterpriseRolesQueryResponse>>]
        public async Task<IActionResult> GetEnterpriseRoles()
        {
            var queryResult = await sender.Send(new GetAllEnterpriseRolesQuery());
            return Ok(queryResult);
        }
        
        [Authorize(Roles = "superAdmin,admin")]
        [HttpGet("auth-routes")]
        [ProducesOkApiResponseType<List<GetAuthorizableRoutesQueryResponse>>]
        public async Task<IActionResult> GetAuthRoutes()
        {
            var queryModel = await sender.Send(new GetAuthorizableRoutesQuery());

            return Ok(queryModel);
        }

       
        [Authorize(Roles = "superAdmin,admin")]
        [HttpPut("update-role-permissions")]
        [ProducesOkApiResponseType]
        public async Task<IActionResult> UpdateRolePermissions(UpdateRoleClaimsCommand model)
        {
            var commandResult =
                await sender.Send(new UpdateRoleClaimsCommand(model.RoleId, model.RoleClaimValue));

            return commandResult.Match(
                response => Ok(response),
                error => error.ToObjectResult()
            );
        }

        [Authorize(Roles = "superAdmin,admin")]
        [HttpPost("new-role")]
        [ProducesOkApiResponseType]
        public async Task<IActionResult> AddRole(AddRoleCommand model)
        {
            var commandResult = await sender.Send(model);

            return commandResult.Match<ActionResult>(
                response => Ok(response),
                error => error.ToObjectResult()
            );
        }
        
    }
}