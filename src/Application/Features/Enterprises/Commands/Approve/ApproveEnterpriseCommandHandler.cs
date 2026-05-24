using System.Text.Json;
using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Enterprises.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.Enterprises;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Enterprises.Commands.Approve;

/// <summary>
/// SuperAdmin-only: flips a Pending enterprise to Active. Sends a "you may log in now" email
/// to every admin user attached to the enterprise (their <see cref="Application.Common.Interfaces.Identity.IEnterpriseAccessGate"/>
/// would otherwise keep returning <see cref="Application.Features.Users.Exceptions.EnterprisePendingApprovalException"/>).
/// </summary>
internal class ApproveEnterpriseCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    IEmailService emailService,
    ICurrentUserService currentUserService,
    IAdminAuditService adminAuditService,
    ILogger<ApproveEnterpriseCommandHandler> logger)
    : IRequestHandler<ApproveEnterpriseCommand, Either<EnterpriseException, Enterprise>>
{
    public async Task<Either<EnterpriseException, Enterprise>> Handle(
        ApproveEnterpriseCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUserService.GetCurrentUserId();
        if (actorId is null)
            return new UnhandledEnterpriseException(request.EnterpriseId,
                new InvalidOperationException("No actor in context."));

        var enterpriseOption = await unitOfWork.EnterpriseRepository
            .GetByIdAsync(request.EnterpriseId, cancellationToken);
        var enterprise = enterpriseOption.Match(e => e, () => (Enterprise?)null);
        if (enterprise is null)
            return new EnterpriseNotFoundException(request.EnterpriseId);

        if (enterprise.Status == EnterpriseStatus.Active)
            return new EnterpriseAlreadyActiveException(request.EnterpriseId);

        if (enterprise.Status != EnterpriseStatus.Pending)
            return new EnterpriseInvalidStateForApprovalException(
                request.EnterpriseId, enterprise.Status.ToString());

        try
        {
            enterprise.Approve(actorId.Value);
            // GetByIdAsync uses TableNoTracking, so the entity is detached. Reattach as
            // Modified so SaveChanges persists the Approve()-set Status / decision fields.
            unitOfWork.EnterpriseRepository.Update(enterprise);

            await adminAuditService.LogAsync(
                action: AuditAction.EnterpriseApproved,
                targetType: AuditTargetType.Enterprise,
                targetId: enterprise.Id,
                targetLabel: $"{enterprise.Name} ({enterprise.Edrpou})",
                enterpriseId: enterprise.Id,
                details: null,
                cancellationToken: cancellationToken);

            await NotifyAdminsAsync(enterprise, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return enterprise;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve enterprise {EnterpriseId}", request.EnterpriseId);
            return new UnhandledEnterpriseException(request.EnterpriseId, ex);
        }
    }

    private async Task NotifyAdminsAsync(Enterprise enterprise, CancellationToken cancellationToken)
    {
        // Find every admin-bearing membership in this enterprise so each receives the
        // "you may log in now" email. Most registrations have exactly one admin, but the
        // flow handles N defensively.
        var memberships = await unitOfWork.UserEnterpriseMembershipRepository
            .GetActiveByUserIdsForEnterpriseAsync(enterprise.Id, cancellationToken);

        var body = EmailTemplates.EnterpriseApprovedToAdmin(enterprise.Name);
        foreach (var membership in memberships)
        {
            var userOption = await userManager.GetUserByIdAsync(membership.UserId);
            var email = userOption.Match(u => u.Email, () => (string?)null);
            if (string.IsNullOrWhiteSpace(email)) continue;

            await emailService.SendEmailAsync(
                toEmail: email,
                subject: $"EcoTrack: реєстрацію підприємства «{enterprise.Name}» підтверджено",
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}
