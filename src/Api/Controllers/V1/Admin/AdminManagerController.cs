using Api.Attributes;
using Api.Controllers.Common;
using Api.Modules.Errors;
using Application.Features.Admin.Queries.GetToken;

using Application.Models.Jwt;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Admin
{
    [ApiVersion("1")]
    [ApiController]
    [Route("api/v{version:apiVersion}/admin-manager")]
    public class AdminManagerController(ISender sender) : BaseController
    {
        [HttpPost("Login")]
        [ProducesOkApiResponseType<AccessToken>]
        public async Task<IActionResult> AdminLogin(AdminGetTokenQuery model)
        {
            var query = await sender.Send(model);

            return query.Match(
                response => Ok(response.Token),
                error => error.ToObjectResult()
            );
        }
    }
}