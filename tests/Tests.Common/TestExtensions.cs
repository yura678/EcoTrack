using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;


namespace Tests.Common;

public static class TestExtensions
{
    public static async Task<T> ToResponseModel<T>(this HttpResponseMessage response)
    {
        var jsonString = await response.Content.ReadAsStringAsync();

        var jsonNode = JsonNode.Parse(jsonString);

        var dataNode = jsonNode?["data"];

        if (dataNode == null)
        {
            throw new ArgumentException("Property 'data' was not found in the response.");
        }

        // Mirror the API's MVC JSON config (SetupModule.AddJsonOptions). The API serializes
        // enums as strings via JsonStringEnumConverter; tests must read them back the same way
        // or every enum-bearing DTO ($.status, $.riskGroup, ...) blows up on deserialize.
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        return dataNode.Deserialize<T>(options)
               ?? throw new ArgumentException("Response data content cannot be null");
    }
}

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static readonly Guid TestUserId = Guid.NewGuid();
    public static readonly Guid TestCompanyId = Guid.NewGuid();

    /// <summary>
    /// Tests can opt out of the default superAdmin role by setting X-Test-Role on the
    /// request — useful for permission-gate verification (e.g. SignalR broadcast denial).
    /// Absent or empty header → default superAdmin which bypasses every DynamicPermission
    /// check.
    /// </summary>
    public const string RoleOverrideHeader = "X-Test-Role";

    /// <summary>
    /// Tests that need a real database User (profile endpoints, change-password, etc.) seed
    /// one with a fresh Guid and pass it via this header so the auth handler issues a JWT
    /// pointing at that exact user. Without the header the default fake TestUserId is used,
    /// which is fine for endpoints that don't touch the User row.
    /// </summary>
    public const string UserIdOverrideHeader = "X-Test-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers.TryGetValue(RoleOverrideHeader, out var headerValue)
                   && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : "superAdmin";

        var userId = Request.Headers.TryGetValue(UserIdOverrideHeader, out var userIdHeader)
                     && Guid.TryParse(userIdHeader, out var parsed)
            ? parsed
            : TestUserId;

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()), // Для GetCurrentUserId()
            new Claim("CompanyId", TestCompanyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}