using System.Net;
using System.Net.Http.Json;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Commands.AcceptInvitation;
using Application.Features.Users.Commands.SendInvitation;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.Enterprises;

namespace Api.Tests.Integration.Users;

/// <summary>
/// Tests the multi-enterprise invitation flow: admin of Enterprise X can invite an email that
/// already belongs to a user registered under Enterprise Y, and the recipient can accept the
/// invitation through the authenticated /accept endpoint without re-registering.
/// Enterprise A == TestAuthHandler.TestCompanyId == the JWT-issuing tenant for these tests.
/// </summary>
public class MultiEnterpriseInvitationTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;

    private Guid _enterpriseAId;
    private Guid _enterpriseBId;
    private Guid _roleAId;
    private Guid _roleBId;

    public MultiEnterpriseInvitationTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    // ─── SendInvitation: existing user can be invited across enterprises ─────────

    [Fact]
    public async Task ShouldAllowInviteWhenExistingUserHasNoMembershipInTargetEnterprise()
    {
        // User U is a member of B only. Admin of A invites U@... — should succeed because U has
        // no membership row for A.
        var email = $"new-tenant-user-{Guid.NewGuid():N}@zavod.ua";
        var userId = await SeedUserAsync(email);
        await SeedMembershipAsync(userId, _enterpriseBId, _roleBId);

        var response = await Client.PostAsJsonAsync(
            "api/v1/users/invite",
            new SendInvitationCommand(email, _roleAId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invite = await Context.Set<EnterpriseInvitation>().AsNoTracking()
            .FirstAsync(i => i.Email == email && i.EnterpriseId == _enterpriseAId);
        invite.IsUsed.Should().BeFalse();
        invite.RoleId.Should().Be(_roleAId);
    }

    [Fact]
    public async Task ShouldRejectInviteWhenUserIsActiveMemberOfTargetEnterprise()
    {
        // User U is already an active member of A; admin of A invites U — must be a 409.
        var email = $"active-{Guid.NewGuid():N}@zavod.ua";
        var userId = await SeedUserAsync(email);
        await SeedMembershipAsync(userId, _enterpriseAId, _roleAId);

        var response = await Client.PostAsJsonAsync(
            "api/v1/users/invite",
            new SendInvitationCommand(email, _roleAId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ShouldRejectInviteWhenUserHasRevokedMembershipInTargetEnterprise()
    {
        // Revoked memberships go through the restore-membership flow; invite must reject so the
        // admin doesn't accidentally create a parallel row (which the unique index would block
        // at the DB layer anyway — but a friendly 409 is better than a 500).
        var email = $"revoked-{Guid.NewGuid():N}@zavod.ua";
        var userId = await SeedUserAsync(email);
        var membership = UserEnterpriseMembership.New(Guid.NewGuid(), userId, _enterpriseAId, _roleAId);
        membership.Revoke();
        await Context.Set<UserEnterpriseMembership>().AddAsync(membership);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            "api/v1/users/invite",
            new SendInvitationCommand(email, _roleAId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── AcceptInvitation: existing-user happy path + guards ─────────────────────

    [Fact]
    public async Task ShouldAcceptInvitationAndCreateMembership()
    {
        var email = $"acceptor-{Guid.NewGuid():N}@zavod.ua";
        var userId = await SeedUserAsync(email);
        await SeedMembershipAsync(userId, _enterpriseAId, _roleAId); // already in A

        // Admin of B (we just write the row directly — no need to spin up a second JWT context)
        // creates an invitation for U to join B.
        var invitation = EnterpriseInvitation.Create(_enterpriseBId, email, _roleBId);
        await Context.Set<EnterpriseInvitation>().AddAsync(invitation);
        await SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/users/invitations/{invitation.Token}/accept");
        request.Headers.Add(TestAuthHandler.UserIdOverrideHeader, userId.ToString());
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var membership = await Context.Set<UserEnterpriseMembership>().AsNoTracking()
            .FirstAsync(m => m.UserId == userId && m.EnterpriseId == _enterpriseBId);
        membership.RoleId.Should().Be(_roleBId);
        membership.RevokedAt.Should().BeNull();

        var reloadedInvite = await Context.Set<EnterpriseInvitation>().AsNoTracking()
            .FirstAsync(i => i.Id == invitation.Id);
        reloadedInvite.IsUsed.Should().BeTrue("accepting consumes the invitation");
    }

    [Fact]
    public async Task ShouldRejectAcceptWhenJwtEmailDoesNotMatchInvitation()
    {
        // User U logged in, invitation is for someone else's email.
        var ownEmail = $"own-{Guid.NewGuid():N}@zavod.ua";
        var userId = await SeedUserAsync(ownEmail);

        var invitation = EnterpriseInvitation.Create(
            _enterpriseBId, $"someone-else-{Guid.NewGuid():N}@zavod.ua", _roleBId);
        await Context.Set<EnterpriseInvitation>().AddAsync(invitation);
        await SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/users/invitations/{invitation.Token}/accept");
        request.Headers.Add(TestAuthHandler.UserIdOverrideHeader, userId.ToString());
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ShouldRejectAcceptWhenUserAlreadyMemberOfInvitationEnterprise()
    {
        // User already has a membership in the invited enterprise — duplicate accept must be a 409.
        var email = $"dup-{Guid.NewGuid():N}@zavod.ua";
        var userId = await SeedUserAsync(email);
        await SeedMembershipAsync(userId, _enterpriseBId, _roleBId);

        var invitation = EnterpriseInvitation.Create(_enterpriseBId, email, _roleBId);
        await Context.Set<EnterpriseInvitation>().AddAsync(invitation);
        await SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/users/invitations/{invitation.Token}/accept");
        request.Headers.Add(TestAuthHandler.UserIdOverrideHeader, userId.ToString());
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var scopedDb = scope.ServiceProvider
            .GetRequiredService<Infrastructure.Persistence.ApplicationDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = email, Email = email,
            EmailConfirmed = true, Name = "T", FamilyName = "U"
        };
        (await um.CreateUser(user, "Pass!Pass1")).Succeeded.Should().BeTrue();
        await scopedDb.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedMembershipAsync(Guid userId, Guid enterpriseId, Guid roleId)
    {
        await Context.Set<UserEnterpriseMembership>().AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), userId, enterpriseId, roleId));
        await SaveChangesAsync();
    }

    public async Task InitializeAsync()
    {
        var sector = SectorsData.FirstTestSector();
        var enterpriseA = Enterprise.NewActive(
            TestAuthHandler.TestCompanyId,
            "Enterprise A", edrpou: $"A-{Guid.NewGuid():N}".Substring(0, 12),
            "Addr A", RiskGroup.Average, sector.Id);
        var enterpriseB = EnterprisesData.SecondTestEquipment(sector.Id);
        await Context.Set<Sector>().AddAsync(sector);
        await Context.Set<Enterprise>().AddRangeAsync(enterpriseA, enterpriseB);

        var roleA = NewRole("inviterole-a", enterpriseA.Id);
        var roleB = NewRole("inviterole-b", enterpriseB.Id);
        await Context.Set<Role>().AddRangeAsync(roleA, roleB);

        _enterpriseAId = enterpriseA.Id;
        _enterpriseBId = enterpriseB.Id;
        _roleAId = roleA.Id;
        _roleBId = roleB.Id;
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();

    private static Role NewRole(string prefix, Guid enterpriseId) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{prefix}-{Guid.NewGuid():N}".Substring(0, 16),
        NormalizedName = $"{prefix}-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
        DisplayName = "Test Role",
        EnterpriseId = enterpriseId
    };
}
