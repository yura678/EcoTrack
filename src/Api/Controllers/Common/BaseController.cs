using System.Security.Claims;
using Api.Modules.Errors;
using Microsoft.AspNetCore.Mvc;
using Shared.Extensions;

namespace Api.Controllers.Common;

public class BaseController : ControllerBase
{
    protected string UserName => User.Identity?.Name;
    protected Guid UserId => User.Identity.GetUserId<Guid>();
    protected string UserEmail => User.Identity.FindFirstValue(ClaimTypes.Email);
    protected string UserRole => User.Identity.FindFirstValue(ClaimTypes.Role);

    // Produce a standard error response inline (for the rare ad-hoc validations that don't go
    // through a domain exception/factory). Routes through the same ErrorBodyBuilder so the
    // envelope shape stays consistent: `message` always set, `data` = { field: [message] } when
    // a fieldName is given, otherwise null. ErrorResultFilterAttribute wraps it.
    protected ObjectResult Error(int statusCode, string message, string? fieldName = null) =>
        ErrorBodyBuilder.Build(statusCode, message, fieldName);
}