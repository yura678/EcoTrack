using System.Net;
using System.Net.Http.Json;
using Application.Common.Interfaces.Identity;
using Domain.Entities.Auditing;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Users;

/// <summary>
/// End-to-end coverage of PUT /users/{id}/email. Admin tenant scoping, email-uniqueness, and
/// the side effects (UserName follow, EmailConfirmed kept, current-tenant tokens killed,
/// UserEmailChanged audit row).
/// </summary>
public class AdminChangeUserEmailTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;
    private readonly string _targetEmail = $"target-{Guid.NewGuid():N}@zavod.ua";

    private Guid _targetUserId;
    private Guid _enterpriseAId;
    private Guid _enterpriseBId;
    private Guid _roleAId;

    public AdminChangeUserEmailTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldReplaceEmailAndUserNameAndKillCurrentTenantTokens()
    {
        var tokenA = await SeedRefreshTokenAsync(_targetUserId, _enterpriseAId);
        var tokenB = await SeedRefreshTokenAsync(_targetUserId, _enterpriseBId);
        var newEmail = $"renamed-{Guid.NewGuid():N}@zavod.ua";

        var response = await SendAsync($"api/v1/users/{_targetUserId}/email",
            new { NewEmail = newEmail }, role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await Context.Set<User>().AsNoTracking().FirstAsync(u => u.Id == _targetUserId);
        user.Email.Should().Be(newEmail);
        user.UserName.Should().Be(newEmail, "username follows email in this codebase");
        user.NormalizedEmail.Should().Be(newEmail.ToUpperInvariant());
        user.EmailConfirmed.Should().BeTrue("admin trust — no reverification needed");

        var validity = await Context.Set<UserRefreshToken>().AsNoTracking()
            .Where(t => t.UserId == _targetUserId)
            .ToDictionaryAsync(t => t.Id, t => t.IsValid);
        validity[tokenA].Should().BeFalse("current-tenant session must die");
        validity[tokenB].Should().BeTrue("session in another tenant survives");

        var audit = await Context.Set<AdminAuditLog>().IgnoreQueryFilters()
            .FirstAsync(r => r.Action == AuditAction.UserEmailChanged && r.TargetId == _targetUserId);
        audit.TargetLabel.Should().Be(newEmail);
        audit.Details.Should().NotBeNull();
        audit.Details!.RootElement.GetProperty("oldEmail").GetString().Should().Be(_targetEmail);
        audit.Details!.RootElement.GetProperty("newEmail").GetString().Should().Be(newEmail);
    }

    [Fact]
    public async Task ShouldRefuseWhenNewEmailAlreadyTaken()
    {
        var conflictEmail = $"taken-{Guid.NewGuid():N}@zavod.ua";
        await SeedUserAsync(conflictEmail);

        var response = await SendAsync($"api/v1/users/{_targetUserId}/email",
            new { NewEmail = conflictEmail }, role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var user = await Context.Set<User>().AsNoTracking().FirstAsync(u => u.Id == _targetUserId);
        user.Email.Should().Be(_targetEmail, "email must not change on conflict");
    }

    [Fact]
    public async Task ShouldReturn404ForUserOutsideTenant()
    {
        var outsider = await SeedUserAsync($"outside-{Guid.NewGuid():N}@zavod.ua");
        await Context.Set<UserEnterpriseMembership>().AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), outsider, _enterpriseBId, _roleAId));
        await SaveChangesAsync();

        var response = await SendAsync($"api/v1/users/{outsider}/email",
            new { NewEmail = $"any-{Guid.NewGuid():N}@zavod.ua" }, role: "admin");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(string url, object body, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(TestAuthHandler.RoleOverrideHeader, role);
        return await Client.SendAsync(request);
    }

    private async Task<Guid> SeedUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = email, Email = email,
            EmailConfirmed = true, Name = "X", FamilyName = "Y"
        };
        (await um.CreateUser(user, "Pass!Pass1")).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private async Task<Guid> SeedRefreshTokenAsync(Guid userId, Guid enterpriseId)
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

    public async Task InitializeAsync()
    {
        var sector = SectorsData.FirstTestSector();
        var enterpriseA = Enterprise.New(
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
