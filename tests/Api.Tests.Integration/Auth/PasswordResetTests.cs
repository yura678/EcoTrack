using System.Net;
using System.Net.Http.Json;
using Application.Common.Interfaces.Identity;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;

namespace Api.Tests.Integration.Auth;

public class PasswordResetTests : BaseIntegrationTest, IAsyncLifetime
{
    private const string OriginalPassword = "Original!Pass1";
    private const string NewPassword = "BrandNew!Pass2";

    // Unique email per test instance — xunit runs tests in parallel by default; sharing a
    // global email would cause concurrent CreateUser races on the same DB.
    private readonly string _email = $"alex-{Guid.NewGuid():N}@zavod.ua";
    private readonly IntegrationTestWebFactory _factory;
    private Guid _userId;

    public PasswordResetTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldSendResetEmailWithToken()
    {
        _factory.Emails.Clear();

        var response = await Client.PostAsJsonAsync(
            "api/v1/auth/forgot-password", new { Email = _email });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Emails.Sent.Should().Contain(e =>
            e.To == _email
            && e.Subject.Contains("Reset")
            && e.Body.Contains("reset-password?email="));
    }

    [Fact]
    public async Task ShouldReturnOkForUnknownEmail()
    {
        _factory.Emails.Clear();

        var response = await Client.PostAsJsonAsync(
            "api/v1/auth/forgot-password", new { Email = "stranger@nowhere.example" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Emails.Sent.Should().BeEmpty(
            "anti-enumeration: unknown emails must look the same as known ones");
    }

    [Fact]
    public async Task ShouldResetPasswordCompleteAuditAndInvalidateRefreshTokens()
    {
        _factory.Emails.Clear();
        await SeedRefreshTokenAsync();

        // Trigger forgot → receive email → extract token.
        await Client.PostAsJsonAsync("api/v1/auth/forgot-password", new { Email = _email });
        var token = ExtractTokenFromLatestEmail();

        var resetResponse = await Client.PostAsJsonAsync(
            "api/v1/auth/reset-password",
            new { Email = _email, Token = token, NewPassword });
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // New password must verify against the hash now stored on the user.
        var newWorks = await VerifyPasswordAsync(NewPassword);
        newWorks.Should().BeTrue();

        // Audit row landed with Completed action targeting this user.
        var rows = await Context.Set<AdminAuditLog>().IgnoreQueryFilters()
            .Where(r => r.Action == AuditAction.UserPasswordResetCompleted
                        && r.TargetId == _userId)
            .ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ActorUserId.Should().Be(_userId, "self-service: actor is the user themselves");

        // Pre-existing refresh tokens flipped to IsValid=false.
        var liveTokens = await Context.Set<UserRefreshToken>()
            .Where(t => t.UserId == _userId && t.IsValid)
            .CountAsync();
        liveTokens.Should().Be(0);
    }

    [Fact]
    public async Task ShouldRejectInvalidToken()
    {
        var response = await Client.PostAsJsonAsync(
            "api/v1/auth/reset-password",
            new { Email = _email, Token = "totally-bogus-token", NewPassword });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task SeedRefreshTokenAsync()
    {
        // EnterpriseId left null — we only need this row to verify InvalidateAllForUserAsync
        // flips it to IsValid=false, and the FK to enterprise is OnDelete=SetNull anyway, so
        // skipping it avoids having to seed a full Enterprise tree for this test.
        await Context.Set<UserRefreshToken>().AddAsync(new UserRefreshToken
        {
            UserId = _userId,
            EnterpriseId = null,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsValid = true
        });
        await SaveChangesAsync();
    }

    private string ExtractTokenFromLatestEmail()
    {
        var email = _factory.Emails.Sent.LastOrDefault();
        email.Should().NotBeNull();
        // Body contains '<a href='https://...?email=X&token=Y'>'
        var hrefStart = email!.Body.IndexOf("href='", StringComparison.Ordinal) + "href='".Length;
        var hrefEnd = email.Body.IndexOf('\'', hrefStart);
        var link = email.Body.Substring(hrefStart, hrefEnd - hrefStart);
        var uri = new Uri(link);
        var queryParts = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return queryParts["token"] ?? throw new InvalidOperationException("token missing from link");
    }

    private async Task<bool> VerifyPasswordAsync(string password)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var user = (await um.GetUserByEmail(_email)).Match(u => u, () => null!);
        return await um.IsPasswordValidAsync(user, password);
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var scopedDb = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = _email,
            Email = _email,
            EmailConfirmed = true,
            Name = "Alex",
            FamilyName = "Petrenko"
        };
        var created = await um.CreateUser(user, OriginalPassword);
        created.Succeeded.Should().BeTrue(
            string.Join("; ", created.Errors.Select(e => e.Description)));
        await scopedDb.SaveChangesAsync();
        _userId = user.Id;
    }

    public Task DisposeAsync()
    {
        // Each test instance uses a unique email + Guid id, so leftover rows in users /
        // user-refresh-tokens / admin-audit-log don't collide with subsequent runs. Skipping
        // explicit cleanup keeps the test simple and avoids ordering pitfalls with FK cascades.
        return Task.CompletedTask;
    }
}

