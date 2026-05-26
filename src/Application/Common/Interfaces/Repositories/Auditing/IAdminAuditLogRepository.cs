using Application.Common.Models;
using Domain.Entities.Auditing;

namespace Application.Common.Interfaces.Repositories.Auditing;

public interface IAdminAuditLogRepository
{
    Task AddAsync(AdminAuditLog entity, CancellationToken cancellationToken);

    /// <summary>
    /// Paged listing newest-first. Filters compose with AND. Tenant isolation is enforced
    /// upstream via the DbContext query filter — callers don't need to pass an EnterpriseId.
    /// `excludeSuperAdminActions` is decided by the controller from the caller's identity
    /// (never by client query string) and hides superAdmin activity from tenant admins.
    /// </summary>
    Task<PageResult<AdminAuditLog>> GetPagedAsync(
        AuditAction? action,
        AuditTargetType? targetType,
        Guid? targetId,
        Guid? actorUserId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        bool excludeSuperAdminActions,
        CancellationToken cancellationToken);
}
