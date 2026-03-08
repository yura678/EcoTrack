using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;

namespace Api.Controllers.Common;

public class BaseController : ControllerBase
{
    protected string UserName => User.Identity?.Name;
    protected Guid UserId => User.Identity.GetUserId<Guid>();
    protected string UserEmail => User.Identity.FindFirstValue(ClaimTypes.Email);
    protected string UserRole => User.Identity.FindFirstValue(ClaimTypes.Role);
    protected string UserKey => User.FindFirstValue(ClaimTypes.UserData);
}