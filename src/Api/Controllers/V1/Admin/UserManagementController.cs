using System.ComponentModel.DataAnnotations;
using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Features.Admin.Commands.AddAdminCommand;
using Application.Features.Users.Commands.SendInvitation;
using Application.Features.Users.Queries.GetUsers;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Admin
{
    [ApiVersion("1")]
    [ApiController]
    [Route("api/v{version:apiVersion}/user-management")]
    [Display(Description = "Managing API Users")]
    public class UserManagementController(ISender sender) : BaseController
    {
        [Authorize(Roles = "admin")]
        [HttpGet("current-enterprise-users")]
        [ProducesOkApiResponseType<List<GetUsersQueryResponse>>]
        public async Task<IActionResult> GetAllEnterpriseUsers()
        {
            var queryResult = await sender.Send(new GetEnterpriseUsersQuery());
            return Ok(queryResult);
        }
        
        [Authorize(Roles = "superAdmin")]
        [HttpGet("current-users")]
        [ProducesOkApiResponseType<List<GetUsersQueryResponse>>]
        public async Task<IActionResult> GetAllUsers()
        {
            var queryResult = await sender.Send(new GetUsersQuery());
            return Ok(queryResult);
        }
        
        [Authorize(Roles = "admin")]
        [HttpPost("invite-user")]
        [ProducesOkApiResponseType<string>]
        public async Task<IActionResult> InviteUser(SendInvitationCommand model)
        {
            var commandResult = await sender.Send(model);

            return commandResult.Match(
                response => Ok(response),
                error => error.ToObjectResult() 
            );
        }
        
        [Authorize(Roles = "superAdmin,admin")]
        [HttpPost("new-user")]
        [ProducesOkApiResponseType]
        public async Task<IActionResult> AddNewUser(AddAdminCommand model)
        {
            var commandResult = await sender.Send(model);
            return commandResult.Match(
                response => Ok(response),
                error => error.ToObjectResult()
            );
        }
    }
}