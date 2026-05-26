using Application.Common.Interfaces.Identity;
using Domain.Common;
using Domain.Entities.Auditing;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Domain.Entities.Notifications;
using Domain.Entities.User;
using Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Extensions;

namespace Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions options,
        ICurrentUserService currentUserService,
        ILogger<ApplicationDbContext>? logger = null)
        : base(options)
    {
        _currentUserService = currentUserService;

        // Diagnostic-only ctor log. The *real* tenant-filter values are computed on every
        // property read (see BypassTenantFilter/TenantFilterId below) because ctor runs
        // during JwtBearer security-stamp lookup, before HttpContext.User is populated —
        // caching them here would lock the whole request into bypass mode.
        logger?.LogInformation(
            "DbContext init (snapshot, values may flip once auth completes): UserId={UserId} IsSuperAdmin={IsSuperAdmin}",
            currentUserService.GetCurrentUserId(), currentUserService.IsSuperAdmin());
    }

    /// <summary>
    /// When true, global query filters skip tenant-id matching.
    /// True for: superAdmin users, system operations (no HttpContext: seed, background jobs, design-time).
    /// <para>
    /// Implemented as a computed property (not a ctor-captured field) because
    /// <see cref="ApplicationDbContext"/> is resolved during JwtBearer authentication for
    /// security-stamp lookup, BEFORE <c>HttpContext.User</c> is populated. Capturing in the
    /// ctor would make every subsequent query in the same scope think the user is
    /// anonymous, bypassing the tenant filter for the entire request. EF Core re-reads
    /// instance members at query time (visible in SQL as
    /// <c>@__ef_filter__BypassTenantFilter_1</c> parameter), so the getter gives a fresh
    /// answer per query.
    /// </para>
    /// </summary>
    public bool BypassTenantFilter =>
        _currentUserService.GetCurrentUserId() is null || _currentUserService.IsSuperAdmin();

    /// <summary>
    /// The EnterpriseId used by global query filters when <see cref="BypassTenantFilter"/> is false.
    /// </summary>
    public Guid? TenantFilterId =>
        BypassTenantFilter ? null : _currentUserService.GetCurrentEnterpriseId();

    /// <summary>
    /// The UserId used by user-scoped query filters when <see cref="BypassTenantFilter"/> is false.
    /// </summary>
    public Guid? CurrentUserFilterId =>
        BypassTenantFilter ? null : _currentUserService.GetCurrentUserId();

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
        modelBuilder.Entity<NotificationSubscription>().HasQueryFilter(x =>
            BypassTenantFilter || x.EnterpriseId == TenantFilterId);
        modelBuilder.Entity<NotificationDelivery>().HasQueryFilter(x =>
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

        // Admin audit log: tenant admins see their own enterprise's rows only; superAdmin and
        // system contexts see everything. Rows with EnterpriseId == null are global actions
        // (only superAdmin should view them, which is exactly what BypassTenantFilter allows).
        modelBuilder.Entity<AdminAuditLog>().HasQueryFilter(x =>
            BypassTenantFilter || x.EnterpriseId == TenantFilterId);
    }
}
