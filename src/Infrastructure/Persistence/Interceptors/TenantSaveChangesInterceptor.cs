using Application.Common.Interfaces.Identity;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.Interceptors;

public class TenantViolationException(string message) : Exception(message);

public class TenantSaveChangesInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Validate(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Validate(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Validate(DbContext? context)
    {
        if (context is null) return;
        if (currentUserService.IsSuperAdmin()) return;

        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return;

        var enterpriseId = currentUserService.GetCurrentEnterpriseId();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not ITenantOwned tenant)
                continue;

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                if (enterpriseId is null || tenant.EnterpriseId != enterpriseId.Value)
                {
                    throw new TenantViolationException(
                        $"Operation rejected: {entry.Entity.GetType().Name} belongs to enterprise {tenant.EnterpriseId} but current enterprise is {enterpriseId?.ToString() ?? "none"}.");
                }
            }

            if (entry.State == EntityState.Modified)
            {
                var original = entry.OriginalValues["EnterpriseId"];
                if (original is Guid originalId && originalId != tenant.EnterpriseId)
                {
                    throw new TenantViolationException(
                        $"EnterpriseId is immutable on {entry.Entity.GetType().Name}.");
                }
            }
        }
    }
}
