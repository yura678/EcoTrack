using Application.Common.Interfaces.Identity;
using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Domain.Entities.User;
using Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;

namespace Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
{
    public ApplicationDbContext(DbContextOptions options, ICurrentUserService currentUserService)
        : base(options)
    {
        var userId = currentUserService.GetCurrentUserId();
        // Bypass tenant isolation for system operations (no HttpContext) and superAdmin
        BypassTenantFilter = userId is null || currentUserService.IsSuperAdmin();
        TenantFilterId = BypassTenantFilter ? null : currentUserService.GetCurrentEnterpriseId();
        CurrentUserFilterId = BypassTenantFilter ? null : userId;
    }

    /// <summary>
    /// When true, global query filters skip tenant-id matching.
    /// True for: superAdmin users, system operations (no HttpContext: seed, background jobs, design-time).
    /// </summary>
    public bool BypassTenantFilter { get; }

    /// <summary>
    /// The EnterpriseId used by global query filters when <see cref="BypassTenantFilter"/> is false.
    /// </summary>
    public Guid? TenantFilterId { get; }

    /// <summary>
    /// The UserId used by user-scoped query filters when <see cref="BypassTenantFilter"/> is false.
    /// </summary>
    public Guid? CurrentUserFilterId { get; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<DateTimeUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>().Metadata.RemoveIndex(
            modelBuilder.Entity<Role>().Property(r => r.NormalizedName).Metadata.GetContainingIndexes().Single()
        );

        modelBuilder.Entity<Role>()
            .HasIndex(r => new { r.NormalizedName, r.EnterpriseId })
            .HasDatabaseName("RoleNameIndex")
            .IsUnique()
            .AreNullsDistinct(false);

        var entitiesAssembly = typeof(IEntity).Assembly;
        modelBuilder.RegisterAllEntities<IEntity>(entitiesAssembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Soft-delete-only filters (global reference data, not tenant-owned)
        modelBuilder.Entity<Sector>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<IedCategory>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<MeasureUnit>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<Pollutant>().HasQueryFilter(x => x.DeletedAt == null);
        modelBuilder.Entity<Enterprise>().HasQueryFilter(x => x.DeletedAt == null);

        // Tenant-owned + soft-deletable
        modelBuilder.Entity<Site>().HasQueryFilter(x =>
            x.DeletedAt == null && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<EmissionSource>().HasQueryFilter(x =>
            x.DeletedAt == null
            && x.Installation!.Site!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));

        // Tenant-owned; visibility cascaded from soft-deletable ancestors.
        modelBuilder.Entity<Installation>().HasQueryFilter(x =>
            x.Site!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<Permit>().HasQueryFilter(x =>
            x.Installation!.Site!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<EmissionLimit>().HasQueryFilter(x =>
            x.Permit!.Installation!.Site!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<Measurement>().HasQueryFilter(x =>
            x.EmissionSource!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<MonitoringDevice>().HasQueryFilter(x =>
            x.Installation!.Site!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<ComplianceEvent>().HasQueryFilter(x =>
            x.EmissionSource!.DeletedAt == null
            && (BypassTenantFilter || x.EnterpriseId == TenantFilterId));
        modelBuilder.Entity<CalibrationRecord>().HasQueryFilter(x =>
            BypassTenantFilter || x.EnterpriseId == TenantFilterId);
        modelBuilder.Entity<DevicePollutantCapability>().HasQueryFilter(x =>
            BypassTenantFilter || x.EnterpriseId == TenantFilterId);

        // Roles: global (EnterpriseId == null, e.g. superAdmin) are visible everywhere; tenant roles
        // visible only inside their enterprise.
        modelBuilder.Entity<Role>().HasQueryFilter(x =>
            BypassTenantFilter || x.EnterpriseId == null || x.EnterpriseId == TenantFilterId);

        // Memberships: row is visible if it belongs to the current user (so cross-tenant flows like
        // SwitchEnterprise / GetMemberships keep working) or to the current enterprise tenant.
        modelBuilder.Entity<UserEnterpriseMembership>().HasQueryFilter(x =>
            BypassTenantFilter || x.UserId == CurrentUserFilterId || x.EnterpriseId == TenantFilterId);

        // Matching filters on dependents so EF doesn't return rows whose required parent is filtered out.
        modelBuilder.Entity<RoleClaim>().HasQueryFilter(x =>
            BypassTenantFilter || x.Role.EnterpriseId == null || x.Role.EnterpriseId == TenantFilterId);

        modelBuilder.Entity<UserRole>().HasQueryFilter(x =>
            BypassTenantFilter || x.Role!.EnterpriseId == null || x.Role!.EnterpriseId == TenantFilterId);

        modelBuilder.Entity<EnterpriseInvitation>().HasQueryFilter(x =>
            x.Enterprise!.DeletedAt == null);
    }
}
