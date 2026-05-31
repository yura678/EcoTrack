using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Application.Common.Interfaces.Identity;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common;
using Tests.Data.Enterprises;
using UserEntity = Domain.Entities.User.User;

namespace Api.Tests.Integration.Enterprises;

/// <summary>
/// End-to-end tests for the Phase-1 enterprise approval gate: a self-service registration
/// lands as Pending, the user can't log in until a SuperAdmin Approves (or sees a clear
/// rejection if Rejected), and every decision lands in the admin audit log + outbox email.
/// </summary>
public class EnterpriseApprovalFlowTests : BaseIntegrationTest, IAsyncLifetime
{
    private readonly IntegrationTestWebFactory _factory;
    private readonly Sector _sector = SectorsData.FirstTestSector();

    private const string BaseRoute = "api/v1/enterprises";

    public EnterpriseApprovalFlowTests(IntegrationTestWebFactory factory) : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShouldListPendingEnterprisesOnly()
    {
        var pending = Enterprise.NewPending(
            Guid.NewGuid(), "Pending Co", "11111111", "Addr", RiskGroup.None, _sector.Id);
        var active = EnterprisesData.SecondTestEquipment(_sector.Id); // NewActive
        await Context.Set<Enterprise>().AddRangeAsync(pending, active);
        await SaveChangesAsync();

        var response = await Client.GetAsync($"{BaseRoute}/pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ToResponseModel<Application.Common.Models.PageResult<EnterpriseDto>>();
        page.Items.Should().ContainSingle(e => e.Id == pending.Id);
        page.Items.Should().NotContain(e => e.Id == active.Id);
        page.Items.Single().Status.Should().Be(EnterpriseStatus.Pending);
    }

    [Fact]
    public async Task ShouldApprovePendingEnterpriseAndNotifyAdmin()
    {
        var (enterprise, adminUser) = await SeedPendingWithAdminAsync("approve@example.com");

        _factory.Emails.Clear();
        var response = await Client.PostAsync($"{BaseRoute}/{enterprise.Id}/approve", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ToResponseModel<EnterpriseDto>();
        dto.Status.Should().Be(EnterpriseStatus.Active);
        dto.ApprovalDecisionAt.Should().NotBeNull();
        dto.ApprovalDecisionByUserId.Should().Be(TestAuthHandler.TestUserId);

        var fresh = await Context.Set<Enterprise>().AsNoTracking()
            .FirstAsync(e => e.Id == enterprise.Id);
        fresh.Status.Should().Be(EnterpriseStatus.Active);

        _factory.Emails.Sent.Should().Contain(e => e.To == adminUser.Email);

        var audit = await Context.Set<AdminAuditLog>().AsNoTracking()
            .Where(a => a.TargetId == enterprise.Id)
            .ToListAsync();
        audit.Should().ContainSingle(a => a.Action == AuditAction.EnterpriseApproved);
    }

    [Fact]
    public async Task ShouldRejectPendingEnterpriseWithReasonAndNotifyAdmin()
    {
        var (enterprise, adminUser) = await SeedPendingWithAdminAsync("reject@example.com");

        _factory.Emails.Clear();
        var response = await Client.PostAsJsonAsync(
            $"{BaseRoute}/{enterprise.Id}/reject",
            new RejectEnterpriseDto("ЄДРПОУ не співпадає з реєстром"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ToResponseModel<EnterpriseDto>();
        dto.Status.Should().Be(EnterpriseStatus.Rejected);
        dto.RejectionReason.Should().Be("ЄДРПОУ не співпадає з реєстром");

        _factory.Emails.Sent.Should().Contain(e =>
            e.To == adminUser.Email && e.Body.Contains("ЄДРПОУ не співпадає з реєстром"));

        var audit = await Context.Set<AdminAuditLog>().AsNoTracking()
            .Where(a => a.TargetId == enterprise.Id)
            .ToListAsync();
        audit.Should().ContainSingle(a => a.Action == AuditAction.EnterpriseRejected);
    }

    [Fact]
    public async Task ApprovingAlreadyActiveEnterpriseIsIdempotent()
    {
        var enterprise = EnterprisesData.FirstTestEquipment(_sector.Id); // NewActive
        await Context.Set<Enterprise>().AddAsync(enterprise);
        await SaveChangesAsync();

        var response = await Client.PostAsync($"{BaseRoute}/{enterprise.Id}/approve", content: null);

        // Approve is idempotent by design (ApproveEnterpriseCommandHandler returns early for an
        // already-Active enterprise, no audit row / email) so the SPA's optimistic mutation stays
        // happy. Re-approving is therefore a no-op success, not a conflict.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ToResponseModel<EnterpriseDto>();
        dto.Status.Should().Be(EnterpriseStatus.Active);
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenRejectingWithoutReason()
    {
        var enterprise = Enterprise.NewPending(
            Guid.NewGuid(), "Pending Co", "22222222", "Addr", RiskGroup.None, _sector.Id);
        await Context.Set<Enterprise>().AddAsync(enterprise);
        await SaveChangesAsync();

        var response = await Client.PostAsJsonAsync(
            $"{BaseRoute}/{enterprise.Id}/reject",
            new RejectEnterpriseDto("   "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EnterpriseAccessGateBlocksUserWhoseOnlyMembershipIsPending()
    {
        var (enterprise, adminUser) = await SeedPendingWithAdminAsync("gated@example.com");

        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IEnterpriseAccessGate>();
        var result = await gate.CheckLoginEligibilityAsync(adminUser.Id, CancellationToken.None);

        result.IsSome.Should().BeTrue();
        var ex = result.Match(e => e, () => throw new Exception("expected exception"));
        ex.Should().BeOfType<EnterprisePendingApprovalException>();
        ex.Message.Should().Contain(enterprise.Edrpou);
    }

    [Fact]
    public async Task EnterpriseAccessGateAllowsUserOnceEnterpriseIsActive()
    {
        var (enterprise, adminUser) = await SeedPendingWithAdminAsync("active@example.com");
        enterprise.Approve(TestAuthHandler.TestUserId);
        Context.Set<Enterprise>().Update(enterprise);
        await SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IEnterpriseAccessGate>();
        var result = await gate.CheckLoginEligibilityAsync(adminUser.Id, CancellationToken.None);

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task EnterpriseAccessGatePrefersRejectedExceptionOverPending()
    {
        var (enterprise, adminUser) = await SeedPendingWithAdminAsync("rejected@example.com");
        enterprise.Reject(TestAuthHandler.TestUserId, "EDRPOU lookup failed");
        Context.Set<Enterprise>().Update(enterprise);
        await SaveChangesAsync();

        using var scope = _factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<IEnterpriseAccessGate>();
        var result = await gate.CheckLoginEligibilityAsync(adminUser.Id, CancellationToken.None);

        var ex = result.Match(e => e, () => throw new Exception("expected exception"));
        ex.Should().BeOfType<EnterpriseRejectedException>();
        ex.Message.Should().Contain("EDRPOU lookup failed");
    }

    private async Task<(Enterprise Enterprise, UserEntity AdminUser)> SeedPendingWithAdminAsync(string email)
    {
        var enterprise = Enterprise.NewPending(
            Guid.NewGuid(), $"Pending Co {email}", $"{Random.Shared.Next(10000000, 99999999)}",
            "Addr", RiskGroup.None, _sector.Id);

        var adminUserId = Guid.NewGuid();
        var adminUser = new UserEntity
        {
            Id = adminUserId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            EmailConfirmed = true
        };

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = $"admin-{enterprise.Id:N}",
            NormalizedName = $"ADMIN-{enterprise.Id:N}".ToUpperInvariant(),
            DisplayName = "Admin",
            EnterpriseId = enterprise.Id
        };

        var membership = UserEnterpriseMembership.New(
            Guid.NewGuid(), adminUserId, enterprise.Id, role.Id);

        await Context.Set<Enterprise>().AddAsync(enterprise);
        await Context.Set<UserEntity>().AddAsync(adminUser);
        await Context.Set<Role>().AddAsync(role);
        await Context.Set<UserEnterpriseMembership>().AddAsync(membership);
        await SaveChangesAsync();
        return (enterprise, adminUser);
    }

    public async Task InitializeAsync()
    {
        await Context.Set<Sector>().AddAsync(_sector);
        await SaveChangesAsync();
    }

    public Task DisposeAsync() => ResetTenantDataAsync();
}
