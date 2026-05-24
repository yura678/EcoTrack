using System.Net;
using Application.Common.Interfaces.Identity;
using Application.Models.Profile;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Users;

public class ProfileSessionsTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly string _email = $"sessions-{Guid.NewGuid():N}@zavod.ua";
    private readonly IntegrationTestWebFactory _factory;
    private Guid _userId;
    private Guid _otherUserId;
    private Guid _enterpriseAId;
    private Guid _enterpriseBId;
    private string _enterpriseAName = "";
    private string _enterpriseBName = "";

    public ProfileSessionsTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldListOnlyCurrentUserActiveSessions()
    {
        var mineA = await SeedTokenAsync(_userId, _enterpriseAId, isValid: true);
        var mineB = await SeedTokenAsync(_userId, _enterpriseBId, isValid: true);
        var mineExpired = await SeedTokenAsync(_userId, _enterpriseAId, isValid: true,
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        var mineRevoked = await SeedTokenAsync(_userId, _enterpriseAId, isValid: false);
        var notMine = await SeedTokenAsync(_otherUserId, _enterpriseAId, isValid: true);

        var response = await SendAsync(HttpMethod.Get, "api/v1/profile/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessions = await response.ToResponseModel<List<SessionInfo>>();
        sessions.Select(s => s.Id).Should().BeEquivalentTo(new[] { mineA, mineB });
        sessions.Should().NotContain(s => s.Id == mineExpired, "expired tokens are not shown");
        sessions.Should().NotContain(s => s.Id == mineRevoked, "revoked tokens are not shown");
        sessions.Should().NotContain(s => s.Id == notMine, "other users' tokens stay private");

        var sessionForA = sessions.Single(s => s.Id == mineA);
        sessionForA.EnterpriseId.Should().Be(_enterpriseAId);
        sessionForA.EnterpriseName.Should().Be(_enterpriseAName);
    }

    [Fact]
    public async Task ShouldRevokeOwnSessionAndIgnoreOthers()
    {
        var mine = await SeedTokenAsync(_userId, _enterpriseAId, isValid: true);
        var notMine = await SeedTokenAsync(_otherUserId, _enterpriseAId, isValid: true);

        var response = await SendAsync(HttpMethod.Delete, $"api/v1/profile/sessions/{mine}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var mineNow = await Context.Set<UserRefreshToken>().AsNoTracking().FirstAsync(t => t.Id == mine);
        mineNow.IsValid.Should().BeFalse();

        // Attempt to revoke a foreign token — handler silently no-ops (idempotent), token stays.
        var attack = await SendAsync(HttpMethod.Delete, $"api/v1/profile/sessions/{notMine}");
        attack.StatusCode.Should().Be(HttpStatusCode.OK,
            "endpoint is idempotent — leaks no information about whether the token existed");

        var notMineNow = await Context.Set<UserRefreshToken>().AsNoTracking().FirstAsync(t => t.Id == notMine);
        notMineNow.IsValid.Should().BeTrue("foreign token must stay live");
    }

    [Fact]
    public async Task ShouldRevokeAllOwnSessionsLeavingOthersAlone()
    {
        await SeedTokenAsync(_userId, _enterpriseAId, isValid: true);
        await SeedTokenAsync(_userId, _enterpriseBId, isValid: true);
        var notMine = await SeedTokenAsync(_otherUserId, _enterpriseAId, isValid: true);

        var response = await SendAsync(HttpMethod.Delete, "api/v1/profile/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var mineLive = await Context.Set<UserRefreshToken>().AsNoTracking()
            .CountAsync(t => t.UserId == _userId && t.IsValid);
        mineLive.Should().Be(0);

        var foreignLive = await Context.Set<UserRefreshToken>().AsNoTracking()
            .Where(t => t.Id == notMine)
            .Select(t => t.IsValid)
            .FirstAsync();
        foreignLive.Should().BeTrue("revoke-all is scoped to the caller, others survive");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.UserIdOverrideHeader, _userId.ToString());
        return await Client.SendAsync(request);
    }

    private async Task<Guid> SeedTokenAsync(
        Guid userId, Guid? enterpriseId, bool isValid, DateTime? expiresAt = null)
    {
        var token = new UserRefreshToken
        {
            UserId = userId,
            EnterpriseId = enterpriseId,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            IsValid = isValid
        };
        await Context.Set<UserRefreshToken>().AddAsync(token);
        await SaveChangesAsync();
        return token.Id;
    }

    public async Task InitializeAsync()
    {
        // Enterprise rows so the FK on user-refresh-tokens.enterprise_id is satisfied + so the
        // GET handler can join in their names.
        var sector = SectorsData.FirstTestSector();
        var enterpriseA = EnterprisesData.FirstTestEquipment(sector.Id);
        var enterpriseB = EnterprisesData.SecondTestEquipment(sector.Id);
        await Context.Set<Sector>().AddAsync(sector);
        await Context.Set<Enterprise>().AddRangeAsync(enterpriseA, enterpriseB);
        await SaveChangesAsync();
        _enterpriseAId = enterpriseA.Id;
        _enterpriseAName = enterpriseA.Name;
        _enterpriseBId = enterpriseB.Id;
        _enterpriseBName = enterpriseB.Name;

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var scopedDb = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(), UserName = _email, Email = _email,
            EmailConfirmed = true, Name = "Owner", FamilyName = "Tester"
        };
        (await um.CreateUser(user, "Pass!Pass1")).Succeeded.Should().BeTrue();
        _userId = user.Id;

        var other = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"other-{Guid.NewGuid():N}@zavod.ua",
            Email = $"other-{Guid.NewGuid():N}@zavod.ua",
            EmailConfirmed = true, Name = "Other", FamilyName = "Person"
        };
        (await um.CreateUser(other, "Pass!Pass1")).Succeeded.Should().BeTrue();
        _otherUserId = other.Id;
        await scopedDb.SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
