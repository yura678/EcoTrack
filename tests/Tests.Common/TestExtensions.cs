using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;


namespace Tests.Common;

public static class TestExtensions
{
    public static WebApplicationFactory<Program> WithWebHostBuilderMock(this IntegrationTestWebFactory factory)
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(defaultScheme: "TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "TestScheme", _ => { });

                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                    options.DefaultScheme = "TestScheme";
                });
            });
        });
    }


    public static async Task<T> ToResponseModel<T>(this HttpResponseMessage response)
    {
        var jsonString = await response.Content.ReadAsStringAsync();

        var jsonNode = JsonNode.Parse(jsonString);

        var dataNode = jsonNode?["data"];

        if (dataNode == null)
        {
            throw new ArgumentException("Property 'data' was not found in the response.");
        }

        return dataNode.Deserialize<T>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Response data content cannot be null");
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

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "superAdmin"),
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()), // Для GetCurrentUserId()
            new Claim("CompanyId", TestCompanyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}