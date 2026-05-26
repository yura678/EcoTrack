using Api.Attributes;
using Api.Controllers.Common;
using Api.Dtos;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Repositories.Auditing;
using Application.Common.Models;
using Asp.Versioning;
using Domain.Entities.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Admin;

/// <summary>
/// Read-only access to the admin audit log. Tenant isolation is enforced by the DbContext
/// query filter: an enterprise admin only sees rows for their own tenant; superAdmin sees all.
/// On top of that, superAdmin activity is hidden from tenant admins (decided from the caller's
/// identity, never from a query-string flag — clients cannot bypass it).
/// </summary>
[ApiVersion("1")]
[ApiController]
[Route("api/v{version:apiVersion}")]
[Authorize(Roles = "admin,superAdmin")]
public class AdminAuditLogController(
    IAdminAuditLogRepository repository,
    ICurrentUserService currentUserService) : BaseController
{
    [HttpGet("admin/audit-log")]
    [ProducesOkApiResponseType<PageResult<AdminAuditLogDto>>]
    public async Task<IActionResult> GetPaged(
        [FromQuery] AdminAuditLogQueryDto query,
        CancellationToken cancellationToken)
    {
        var excludeSuperAdmin = !currentUserService.IsSuperAdmin();
        var result = await repository.GetPagedAsync(
            query.Action, query.TargetType, query.TargetId, query.ActorUserId,
            query.From, query.To, query.Page, query.PageSize,
            excludeSuperAdmin, cancellationToken);

        return Ok(new PageResult<AdminAuditLogDto>
        {
            Items = result.Items.Select(AdminAuditLogDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    /// <summary>
    /// Convenience filter: every row where TargetType=User AND TargetId=userId. Used by the
    /// admin user-profile screen to render its audit timeline.
    /// </summary>
    [HttpGet("users/{userId:guid}/audit")]
    [ProducesOkApiResponseType<PageResult<AdminAuditLogDto>>]
    public async Task<IActionResult> GetForUser(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var excludeSuperAdmin = !currentUserService.IsSuperAdmin();
        var result = await repository.GetPagedAsync(
            action: null, targetType: AuditTargetType.User, targetId: userId,
            actorUserId: null, from: null, to: null,
            page: page, pageSize: pageSize,
            excludeSuperAdminActions: excludeSuperAdmin,
            cancellationToken: cancellationToken);

        return Ok(new PageResult<AdminAuditLogDto>
        {
            Items = result.Items.Select(AdminAuditLogDto.FromDomainModel).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }
}
