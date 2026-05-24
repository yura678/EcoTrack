using System.Net;
using System.Net.Http.Json;
using Application.Common.Interfaces.Identity;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Auth;

/// <summary>
/// End-to-end coverage that revoking a membership / changing a role through the real HTTP
/// pipeline kills the affected tenant's refresh tokens immediately — but leaves the same
/// user's sessions in other tenants alone. This is the security-critical invariant that justifies
/// the per-tenant InvalidateAllForUserAndEnterpriseAsync repo method existing in the first place.
/// </summary>
public class RefreshTokenInvalidationTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;
    private readonly string _email = $"target-{Guid.NewGuid():N}@zavod.ua";

    private Guid _targetUserId;
    private Guid _enterpriseA;       // tenant the admin acts within (== TestCompanyId)
    private Guid _enterpriseB;       // unrelated tenant — its sessions must survive
    private Guid _roleA;             // some role in enterprise A — needed for ChangeMemberRole test

    public RefreshTokenInvalidationTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RevokeMembershipShouldKillTokensForThatEnterpriseOnly()
    {
        var tokenA = await SeedRefreshTokenAsync(_enterpriseA);
        var tokenB = await SeedRefreshTokenAsync(_enterpriseB);

        var response = await SendAsync(
            HttpMethod.Delete,
            $"api/v1/users/{_targetUserId}/membership");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await LoadTokenValidityAsync();
        tokens[tokenA].Should().BeFalse(
            "revoking from enterprise A must invalidate the user's session for A");
        tokens[tokenB].Should().BeTrue(
            "session for an unrelated enterprise B must survive — admin of A cannot log the user out of B");
    }

    [Fact]
    public async Task ChangeMemberRoleShouldKillTokensForThatEnterpriseOnly()
    {
        var tokenA = await SeedRefreshTokenAsync(_enterpriseA);
        var tokenB = await SeedRefreshTokenAsync(_enterpriseB);

        var response = await SendAsync(
            HttpMethod.Put,
            $"api/v1/users/{_targetUserId}/role",
            new { RoleId = _roleA });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokens = await LoadTokenValidityAsync();
        tokens[tokenA].Should().BeFalse(
            "role change in enterprise A forces re-login for that tenant");
        tokens[tokenB].Should().BeTrue(
            "role change in A must not log the user out of B");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.RoleOverrideHeader, "admin");
        if (body is not null) request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    private async Task<Guid> SeedRefreshTokenAsync(Guid enterpriseId)
    {
        var token = new UserRefreshToken
        {
            UserId = _targetUserId,
            EnterpriseId = enterpriseId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsValid = true
        };
        await Context.Set<UserRefreshToken>().AddAsync(token);
        await SaveChangesAsync();
        return token.Id;
    }

    private async Task<Dictionary<Guid, bool>> LoadTokenValidityAsync() =>
        await Context.Set<UserRefreshToken>()
            .Where(t => t.UserId == _targetUserId)
            .ToDictionaryAsync(t => t.Id, t => t.IsValid);

    public async Task InitializeAsync()
    {
        // Enterprise A must have Id = TestCompanyId so the admin auth context (CompanyId claim
        // wired by TestAuthHandler) addresses an enterprise that actually exists. Without this
        // the handler's GetCurrentEnterpriseId() would point at a phantom tenant and the
        // membership lookup would always miss.
        var sector = SectorsData.FirstTestSector();
        var enterpriseA = Enterprise.New(
            TestAuthHandler.TestCompanyId,
            "Enterprise A", edrpou: $"A-{Guid.NewGuid():N}".Substring(0, 12),
            "Addr A", RiskGroup.Average, sector.Id);
        var enterpriseB = Enterprise.New(
            Guid.NewGuid(),
            "Enterprise B", edrpou: $"B-{Guid.NewGuid():N}".Substring(0, 12),
            "Addr B", RiskGroup.Average, sector.Id);
        await Context.Set<Sector>().AddAsync(sector);
        await Context.Set<Enterprise>().AddRangeAsync(enterpriseA, enterpriseB);
        _enterpriseA = enterpriseA.Id;
        _enterpriseB = enterpriseB.Id;

        var roleA = new Role
        {
            Id = Guid.NewGuid(),
            Name = $"viewer-{Guid.NewGuid():N}".Substring(0, 16),
            NormalizedName = $"VIEWER-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            DisplayName = "Viewer for A",
            EnterpriseId = _enterpriseA
        };
        await Context.Set<Role>().AddAsync(roleA);
        _roleA = roleA.Id;
        await SaveChangesAsync();

        // Target user — the human whose memberships we'll mutate via the admin endpoints.
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
            Name = "Target",
            FamilyName = "User"
        };
        var created = await um.CreateUser(user, "TestPass!1");
        created.Succeeded.Should().BeTrue(
            string.Join("; ", created.Errors.Select(e => e.Description)));
        await scopedDb.SaveChangesAsync();
        _targetUserId = user.Id;

        // Active membership in A so the admin endpoints find a row to mutate. Membership in B
        // isn't needed for the test — we only need the refresh token row pointing at B to
        // observe non-interference, and the per-tenant invalidation key is (UserId, EnterpriseId).
        await Context.Set<UserEnterpriseMembership>().AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), _targetUserId, _enterpriseA, _roleA));
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
