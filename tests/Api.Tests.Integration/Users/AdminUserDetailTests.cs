using System.Net;
using Application.Common.Interfaces.Identity;
using Application.Models.Users;
using Domain.Entities.Auditing;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Users;

/// <summary>
/// Phase 3 admin endpoints around a single user: detail page, unlock, sessions revoke.
/// All three share tenant scoping — admin sees user only via active membership in their
/// enterprise (Enterprise A here, matching TestCompanyId).
/// </summary>
public class AdminUserDetailTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;
    private readonly string _targetEmail = $"target-{Guid.NewGuid():N}@zavod.ua";

    private Guid _targetUserId;
    private Guid _enterpriseAId;
    private Guid _enterpriseBId;
    private Guid _roleAId;

    public AdminUserDetailTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldReturnUserDetailForTenantMember()
    {
        var response = await SendAsync(HttpMethod.Get, $"api/v1/users/{_targetUserId}", role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.ToResponseModel<UserDetailInfo>();
        detail.UserId.Should().Be(_targetUserId);
        detail.Email.Should().Be(_targetEmail);
        detail.Membership.Should().NotBeNull("target user has an active membership in admin's tenant");
        detail.Membership!.EnterpriseId.Should().Be(_enterpriseAId);
        detail.Membership.RoleId.Should().Be(_roleAId);
        detail.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldReturn404ForUserOutsideTenant()
    {
        // Create a user whose only membership is in enterprise B; admin of A must not see them.
        var outsider = await SeedUserAsync(email: $"outside-{Guid.NewGuid():N}@zavod.ua");
        await Context.Set<UserEnterpriseMembership>().AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), outsider, _enterpriseBId, _roleAId));
        await SaveChangesAsync();

        var response = await SendAsync(HttpMethod.Get, $"api/v1/users/{outsider}", role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ShouldUnlockUserAndAuditTheAction()
    {
        await LockUserAsync(_targetUserId);

        var response = await SendAsync(HttpMethod.Post, $"api/v1/users/{_targetUserId}/unlock", role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await Context.Set<User>().AsNoTracking().FirstAsync(u => u.Id == _targetUserId);
        user.LockoutEnd.Should().BeNull("unlock clears LockoutEnd");
        user.AccessFailedCount.Should().Be(0, "unlock resets failed-attempt counter");

        var auditRow = await Context.Set<AdminAuditLog>().IgnoreQueryFilters()
            .Where(r => r.Action == AuditAction.UserAccountUnlocked && r.TargetId == _targetUserId)
            .FirstAsync();
        auditRow.EnterpriseId.Should().Be(TestAuthHandler.TestCompanyId);
    }

    [Fact]
    public async Task ShouldRevokeOnlyCurrentTenantSessions()
    {
        var tokenA1 = await SeedRefreshTokenAsync(_targetUserId, _enterpriseAId);
        var tokenA2 = await SeedRefreshTokenAsync(_targetUserId, _enterpriseAId);
        var tokenB = await SeedRefreshTokenAsync(_targetUserId, _enterpriseBId);

        var response = await SendAsync(HttpMethod.Delete, $"api/v1/users/{_targetUserId}/sessions", role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var validity = await Context.Set<UserRefreshToken>().AsNoTracking()
            .Where(t => t.UserId == _targetUserId)
            .ToDictionaryAsync(t => t.Id, t => t.IsValid);
        validity[tokenA1].Should().BeFalse("session in admin's tenant must die");
        validity[tokenA2].Should().BeFalse();
        validity[tokenB].Should().BeTrue("session in another tenant must survive");

        var auditRow = await Context.Set<AdminAuditLog>().IgnoreQueryFilters()
            .Where(r => r.Action == AuditAction.UserSessionsRevoked && r.TargetId == _targetUserId)
            .FirstAsync();
        auditRow.EnterpriseId.Should().Be(TestAuthHandler.TestCompanyId);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string role)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.RoleOverrideHeader, role);
        return await Client.SendAsync(request);
    }

    private async Task<Guid> SeedRefreshTokenAsync(Guid userId, Guid? enterpriseId)
    {
        var token = new UserRefreshToken
        {
            UserId = userId,
            EnterpriseId = enterpriseId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsValid = true
        };
        await Context.Set<UserRefreshToken>().AddAsync(token);
        await SaveChangesAsync();
        return token.Id;
    }

    private async Task LockUserAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var raw = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await raw.FindByIdAsync(userId.ToString());
        await raw.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1));
        await raw.AccessFailedAsync(user!);
    }

    private async Task<Guid> SeedUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var scopedDb = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = email, Email = email,
            EmailConfirmed = true, Name = "X", FamilyName = "Y"
        };
        (await um.CreateUser(user, "Pass!Pass1")).Succeeded.Should().BeTrue();
        await scopedDb.SaveChangesAsync();
        return user.Id;
    }

    public async Task InitializeAsync()
    {
        // Enterprise A = the admin's tenant (matches TestCompanyId so JWT CompanyId claim points
        // at a real row). Enterprise B exists so we can demonstrate cross-tenant isolation.
        var sector = SectorsData.FirstTestSector();
        var enterpriseA = Enterprise.NewActive(
            TestAuthHandler.TestCompanyId,
            "Enterprise A", edrpou: $"A-{Guid.NewGuid():N}".Substring(0, 12),
            "Addr A", RiskGroup.Average, sector.Id);
        var enterpriseB = EnterprisesData.SecondTestEquipment(sector.Id);
        await Context.Set<Sector>().AddAsync(sector);
        await Context.Set<Enterprise>().AddRangeAsync(enterpriseA, enterpriseB);

        var roleA = new Role
        {
            Id = Guid.NewGuid(),
            Name = $"viewer-{Guid.NewGuid():N}".Substring(0, 16),
            NormalizedName = $"VIEWER-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            DisplayName = "Viewer",
            EnterpriseId = enterpriseA.Id
        };
        await Context.Set<Role>().AddAsync(roleA);
        _enterpriseAId = enterpriseA.Id;
        _enterpriseBId = enterpriseB.Id;
        _roleAId = roleA.Id;
        await SaveChangesAsync();

        _targetUserId = await SeedUserAsync(_targetEmail);
        await Context.Set<UserEnterpriseMembership>().AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), _targetUserId, _enterpriseAId, _roleAId));
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
