using Domain.Common;
using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.Interceptors;

/// <summary>
/// Resolves EnterpriseId on Added ITenantOwned entities by walking parent FK navigation,
/// so handlers/seed don't need to set it explicitly. Lookups go through ChangeTracker first,
/// falling back to a DB query (cached for the remainder of the SaveChanges call).
/// </summary>
public class TenantBackfillInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is ApplicationDbContext ctx)
        {
            BackfillAsync(ctx).GetAwaiter().GetResult();
        }
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext ctx)
        {
            await BackfillAsync(ctx, cancellationToken);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static async Task BackfillAsync(ApplicationDbContext ctx, CancellationToken ct = default)
    {
        var entries = ctx.ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added && e.Entity is ITenantOwned)
            .ToList();

        if (entries.Count == 0) return;

        foreach (var entry in entries)
        {
            var tenant = (ITenantOwned)entry.Entity;
            if (tenant.EnterpriseId != Guid.Empty) continue;

            var enterpriseId = await ResolveEnterpriseIdAsync(ctx, entry, ct);
            if (enterpriseId is null) continue;

            tenant.AssignTenant(enterpriseId.Value);
        }
    }

    private static async Task<Guid?> ResolveEnterpriseIdAsync(
        ApplicationDbContext ctx, EntityEntry entry, CancellationToken ct)
    {
        switch (entry.Entity)
        {
            case Site:
                return null; // Site root must already have EnterpriseId set explicitly.

            case Installation installation:
                return await LookupSiteEnterpriseAsync(ctx, installation.SiteId, ct);

            case EmissionSource source:
                return await LookupInstallationEnterpriseAsync(ctx, source.InstallationId, ct);

            case Permit permit:
                return await LookupInstallationEnterpriseAsync(ctx, permit.InstallationId, ct);

            case EmissionLimit limit:
                return await LookupPermitEnterpriseAsync(ctx, limit.PermitId, ct);

            case MonitoringDevice device:
                return await LookupInstallationEnterpriseAsync(ctx, device.InstallationId, ct);

            case Measurement measurement:
                return await LookupEmissionSourceEnterpriseAsync(ctx, measurement.EmissionSourceId, ct);

            case ComplianceEvent complianceEvent:
                return await LookupEmissionSourceEnterpriseAsync(ctx, complianceEvent.EmissionSourceId, ct);

            case CalibrationRecord calibration:
                return await LookupDeviceEnterpriseAsync(ctx, calibration.DeviceId, ct);

            case DevicePollutantCapability capability:
                return await LookupDeviceEnterpriseAsync(ctx, capability.DeviceId, ct);

            default:
                return null;
        }
    }

    private static async Task<Guid?> LookupSiteEnterpriseAsync(
        ApplicationDbContext ctx, Guid siteId, CancellationToken ct)
    {
        var tracked = ctx.ChangeTracker.Entries<Site>()
            .FirstOrDefault(e => e.Entity.Id == siteId)?.Entity;
        if (tracked is not null) return tracked.EnterpriseId;

        return await ctx.Set<Site>().IgnoreQueryFilters()
            .Where(s => s.Id == siteId)
            .Select(s => (Guid?)s.EnterpriseId)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Guid?> LookupInstallationEnterpriseAsync(
        ApplicationDbContext ctx, Guid installationId, CancellationToken ct)
    {
        var tracked = ctx.ChangeTracker.Entries<Installation>()
            .FirstOrDefault(e => e.Entity.Id == installationId)?.Entity;
        if (tracked is not null && tracked.EnterpriseId != Guid.Empty) return tracked.EnterpriseId;
        if (tracked is not null) return await LookupSiteEnterpriseAsync(ctx, tracked.SiteId, ct);

        return await ctx.Set<Installation>().IgnoreQueryFilters()
            .Where(i => i.Id == installationId)
            .Select(i => (Guid?)i.EnterpriseId)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Guid?> LookupPermitEnterpriseAsync(
        ApplicationDbContext ctx, Guid permitId, CancellationToken ct)
    {
        var tracked = ctx.ChangeTracker.Entries<Permit>()
            .FirstOrDefault(e => e.Entity.Id == permitId)?.Entity;
        if (tracked is not null && tracked.EnterpriseId != Guid.Empty) return tracked.EnterpriseId;
        if (tracked is not null) return await LookupInstallationEnterpriseAsync(ctx, tracked.InstallationId, ct);

        return await ctx.Set<Permit>().IgnoreQueryFilters()
            .Where(p => p.Id == permitId)
            .Select(p => (Guid?)p.EnterpriseId)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Guid?> LookupEmissionSourceEnterpriseAsync(
        ApplicationDbContext ctx, Guid sourceId, CancellationToken ct)
    {
        var tracked = ctx.ChangeTracker.Entries<EmissionSource>()
            .FirstOrDefault(e => e.Entity.Id == sourceId)?.Entity;
        if (tracked is not null && tracked.EnterpriseId != Guid.Empty) return tracked.EnterpriseId;
        if (tracked is not null) return await LookupInstallationEnterpriseAsync(ctx, tracked.InstallationId, ct);

        return await ctx.Set<EmissionSource>().IgnoreQueryFilters()
            .Where(s => s.Id == sourceId)
            .Select(s => (Guid?)s.EnterpriseId)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Guid?> LookupDeviceEnterpriseAsync(
        ApplicationDbContext ctx, Guid deviceId, CancellationToken ct)
    {
        var tracked = ctx.ChangeTracker.Entries<MonitoringDevice>()
            .FirstOrDefault(e => e.Entity.Id == deviceId)?.Entity;
        if (tracked is not null && tracked.EnterpriseId != Guid.Empty) return tracked.EnterpriseId;
        if (tracked is not null) return await LookupInstallationEnterpriseAsync(ctx, tracked.InstallationId, ct);

        return await ctx.Set<MonitoringDevice>().IgnoreQueryFilters()
            .Where(d => d.Id == deviceId)
            .Select(d => (Guid?)d.EnterpriseId)
            .FirstOrDefaultAsync(ct);
    }
}
