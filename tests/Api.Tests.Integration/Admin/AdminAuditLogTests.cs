using System.Net;
using System.Text.Json;
using Api.Dtos;
using Application.Common.Models;
using Domain.Entities.Auditing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Common;

namespace Api.Tests.Integration.Admin;

public class AdminAuditLogTests : BaseIntegrationTest, IAsyncLifetime
{
    public AdminAuditLogTests(IntegrationTestWebFactory factory) : base(factory) { }

    [Fact]
    public async Task ShouldReturnSeededRowsForCurrentTenant()
    {
        var mine = SeedAuditRow(
            enterpriseId: TestAuthHandler.TestCompanyId,
            action: AuditAction.UserInvited,
            targetType: AuditTargetType.Invitation,
            targetLabel: "alex@zavod.ua");
        var foreign = SeedAuditRow(
            enterpriseId: Guid.NewGuid(),
            action: AuditAction.UserInvited,
            targetType: AuditTargetType.Invitation,
            targetLabel: "stranger@other.co");
        await Context.Set<AdminAuditLog>().AddRangeAsync(mine, foreign);
        await SaveChangesAsync();

        // X-Test-Role=admin → tenant filter applies; superAdmin would bypass and see both.
        var page = await GetPagedAsync("admin/audit-log", role: "admin");
        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(r => r.Id == mine.Id);
        page.Items.Should().NotContain(r => r.Id == foreign.Id);
    }

    [Fact]
    public async Task ShouldFilterByActionAndTarget()
    {
        var targetUser = Guid.NewGuid();
        var roleChange = SeedAuditRow(
            enterpriseId: TestAuthHandler.TestCompanyId,
            action: AuditAction.UserRoleChanged,
            targetType: AuditTargetType.User,
            targetId: targetUser);
        var unrelatedInvite = SeedAuditRow(
            enterpriseId: TestAuthHandler.TestCompanyId,
            action: AuditAction.UserInvited,
            targetType: AuditTargetType.Invitation);
        await Context.Set<AdminAuditLog>().AddRangeAsync(roleChange, unrelatedInvite);
        await SaveChangesAsync();

        var filtered = await GetPagedAsync(
            $"admin/audit-log?action={AuditAction.UserRoleChanged}");
        filtered.Items.Should().ContainSingle()
            .Which.Id.Should().Be(roleChange.Id);
    }

    [Fact]
    public async Task ShouldReturnRowsForUserConvenienceEndpoint()
    {
        var userId = Guid.NewGuid();
        var matching = SeedAuditRow(
            enterpriseId: TestAuthHandler.TestCompanyId,
            action: AuditAction.UserRoleChanged,
            targetType: AuditTargetType.User,
            targetId: userId);
        var unrelated = SeedAuditRow(
            enterpriseId: TestAuthHandler.TestCompanyId,
            action: AuditAction.UserRoleChanged,
            targetType: AuditTargetType.User,
            targetId: Guid.NewGuid());
        await Context.Set<AdminAuditLog>().AddRangeAsync(matching, unrelated);
        await SaveChangesAsync();

        var page = await GetPagedAsync($"users/{userId}/audit");
        page.Items.Should().ContainSingle()
            .Which.Id.Should().Be(matching.Id);
    }

    [Fact]
    public async Task ShouldExposeDetailsAsJsonElement()
    {
        var row = SeedAuditRow(
            enterpriseId: TestAuthHandler.TestCompanyId,
            action: AuditAction.UserInvited,
            targetType: AuditTargetType.Invitation,
            details: JsonSerializer.SerializeToDocument(new { roleId = "viewer", reason = "onboarding" }));
        await Context.Set<AdminAuditLog>().AddAsync(row);
        await SaveChangesAsync();

        var page = await GetPagedAsync("admin/audit-log");
        var dto = page.Items.Single(r => r.Id == row.Id);
        dto.Details.Should().NotBeNull();
        dto.Details!.Value.GetProperty("roleId").GetString().Should().Be("viewer");
        dto.Details!.Value.GetProperty("reason").GetString().Should().Be("onboarding");
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private static AdminAuditLog SeedAuditRow(
        Guid? enterpriseId,
        AuditAction action,
        AuditTargetType targetType,
        Guid? targetId = null,
        string? targetLabel = null,
        JsonDocument? details = null)
    {
        return AdminAuditLog.Create(
            enterpriseId: enterpriseId,
            actorUserId: Guid.NewGuid(),
            actorEmail: "actor@test.local",
            actorRole: "admin",
            action: action,
            targetType: targetType,
            targetId: targetId,
            targetLabel: targetLabel,
            details: details,
            ipAddress: "127.0.0.1",
            userAgent: "xUnit");
    }

    private async Task<PageResult<AdminAuditLogDto>> GetPagedAsync(string path, string? role = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/{path}");
        if (!string.IsNullOrEmpty(role))
            request.Headers.Add(TestAuthHandler.RoleOverrideHeader, role);
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ToResponseModel<PageResult<AdminAuditLogDto>>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE audit.\"admin-audit-log\" RESTART IDENTITY;");
    }
}
