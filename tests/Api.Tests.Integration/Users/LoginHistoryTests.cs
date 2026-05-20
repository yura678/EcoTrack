using System.Net;
using System.Net.Http.Json;
using Application.Common.Models;
using Application.Models.Profile;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Application.Common.Interfaces.Identity;
using Domain.Entities.Enterprises;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Users;

/// <summary>
/// End-to-end Phase-4 coverage. Real HTTP login flows write LoginAttempt rows; the self-view
/// and admin-view endpoints surface those rows with proper scoping.
/// </summary>
public class LoginHistoryTests : BaseIntegrationTest, IAsyncLifetime
{
    private const string Password = "Original!Pass1";
    private readonly string _email = $"hist-{Guid.NewGuid():N}@zavod.ua";
    private readonly IntegrationTestWebFactory _factory;
    private Guid _userId;
    private Guid _enterpriseAId;
    private Guid _roleAId;

    public LoginHistoryTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldRecordSuccessAndInvalidCredentialsViaLoginByPassword()
    {
        // Three login attempts via the real auth endpoint — one good, one wrong password,
        // one unknown email. All three should land in login-attempts with the right outcome.
        await PostLoginPasswordAsync(_email, Password);          // Success
        await PostLoginPasswordAsync(_email, "BogusPass!1");     // InvalidCredentials
        await PostLoginPasswordAsync("ghost@nowhere.example", "x"); // UnknownEmail

        var attempts = await Context.Set<LoginAttempt>().AsNoTracking()
            .Where(a => a.EmailAttempted == _email || a.EmailAttempted == "ghost@nowhere.example")
            .ToListAsync();
        attempts.Should().HaveCount(3);
        attempts.Should().Contain(a => a.UserId == _userId && a.Outcome == LoginOutcome.Success);
        attempts.Should().Contain(a => a.UserId == _userId && a.Outcome == LoginOutcome.InvalidCredentials);
        attempts.Should().Contain(a => a.UserId == null && a.Outcome == LoginOutcome.UnknownEmail);
        attempts.Should().OnlyContain(a => a.Method == LoginMethod.Password);
    }

    [Fact]
    public async Task ShouldReturnOnlyOwnHistoryFromProfileEndpoint()
    {
        var otherUserId = await SeedExtraUserAsync();
        await SeedAttemptAsync(_userId, LoginOutcome.Success);
        await SeedAttemptAsync(_userId, LoginOutcome.InvalidCredentials);
        await SeedAttemptAsync(otherUserId, LoginOutcome.Success);

        var response = await SendAsync(HttpMethod.Get, "api/v1/profile/login-history");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.ToResponseModel<PageResult<LoginHistoryEntry>>();
        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(e =>
            e.Outcome == LoginOutcome.Success || e.Outcome == LoginOutcome.InvalidCredentials);
    }

    [Fact]
    public async Task AdminCanReadLoginHistoryOfTenantMember()
    {
        await SeedAttemptAsync(_userId, LoginOutcome.Success);
        await SeedAttemptAsync(_userId, LoginOutcome.InvalidCredentials);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"api/v1/users/{_userId}/login-history");
        request.Headers.Add(TestAuthHandler.RoleOverrideHeader, "admin");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.ToResponseModel<PageResult<LoginHistoryEntry>>();
        page.TotalCount.Should().Be(2);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostLoginPasswordAsync(string email, string password)
    {
        return await Client.PostAsJsonAsync(
            "api/v1/auth/login/password", new { Email = email, Password = password });
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.UserIdOverrideHeader, _userId.ToString());
        return await Client.SendAsync(request);
    }

    private async Task SeedAttemptAsync(Guid userId, LoginOutcome outcome)
    {
        await Context.Set<LoginAttempt>().AddAsync(LoginAttempt.Create(
            userId: userId,
            emailAttempted: "seed@test.local",
            ipAddress: "127.0.0.1",
            userAgent: "xUnit",
            method: LoginMethod.Password,
            outcome: outcome));
        await SaveChangesAsync();
    }

    private async Task<Guid> SeedExtraUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var email = $"other-{Guid.NewGuid():N}@zavod.ua";
        var u = new User
        {
            Id = Guid.NewGuid(), UserName = email, Email = email,
            EmailConfirmed = true, Name = "X", FamilyName = "Y"
        };
        (await um.CreateUser(u, Password)).Succeeded.Should().BeTrue();
        return u.Id;
    }

    public async Task InitializeAsync()
    {
        // Enterprise A = matches TestCompanyId so admin context lookups succeed for the
        // admin-side history test.
        var sector = SectorsData.FirstTestSector();
        var enterpriseA = Enterprise.New(
            TestAuthHandler.TestCompanyId,
            "Enterprise A", edrpou: $"A-{Guid.NewGuid():N}".Substring(0, 12),
            "Addr A", RiskGroup.Average, sector.Id);
        await Context.Set<Sector>().AddAsync(sector);
        await Context.Set<Enterprise>().AddAsync(enterpriseA);
        _enterpriseAId = enterpriseA.Id;

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = $"viewer-{Guid.NewGuid():N}".Substring(0, 16),
            NormalizedName = $"VIEWER-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            DisplayName = "Viewer",
            EnterpriseId = enterpriseA.Id
        };
        await Context.Set<Role>().AddAsync(role);
        _roleAId = role.Id;
        await SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = _email, Email = _email,
            EmailConfirmed = true, Name = "Hist", FamilyName = "User"
        };
        (await um.CreateUser(user, Password)).Succeeded.Should().BeTrue();
        _userId = user.Id;

        await Context.Set<UserEnterpriseMembership>().AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), _userId, _enterpriseAId, _roleAId));
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
