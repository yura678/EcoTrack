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

namespace Application.Features.Enterprises.Commands.Reject;

internal class RejectEnterpriseCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    IEmailService emailService,
    ICurrentUserService currentUserService,
    IAdminAuditService adminAuditService,
    ILogger<RejectEnterpriseCommandHandler> logger)
    : IRequestHandler<RejectEnterpriseCommand, Either<EnterpriseException, Enterprise>>
{
    public async Task<Either<EnterpriseException, Enterprise>> Handle(
        RejectEnterpriseCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return new EnterpriseRejectionReasonRequiredException(request.EnterpriseId);

        var actorId = currentUserService.GetCurrentUserId();
        if (actorId is null)
            return new UnhandledEnterpriseException(request.EnterpriseId,
                new InvalidOperationException("No actor in context."));

        var enterpriseOption = await unitOfWork.EnterpriseRepository
            .GetByIdAsync(request.EnterpriseId, cancellationToken);
        var enterprise = enterpriseOption.Match(e => e, () => (Enterprise?)null);
        if (enterprise is null)
            return new EnterpriseNotFoundException(request.EnterpriseId);

        try
        {
            enterprise.Reject(actorId.Value, request.Reason);
            // GetByIdAsync uses TableNoTracking, so the entity is detached. Reattach as
            // Modified so SaveChanges persists the Reject()-set Status / decision fields.
            unitOfWork.EnterpriseRepository.Update(enterprise);

            var detailsJson = JsonDocument.Parse(
                JsonSerializer.Serialize(new { reason = enterprise.RejectionReason }));

            await adminAuditService.LogAsync(
                action: AuditAction.EnterpriseRejected,
                targetType: AuditTargetType.Enterprise,
                targetId: enterprise.Id,
                targetLabel: $"{enterprise.Name} ({enterprise.Edrpou})",
                enterpriseId: enterprise.Id,
                details: detailsJson,
                cancellationToken: cancellationToken);

            await NotifyAdminsAsync(enterprise, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return enterprise;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reject enterprise {EnterpriseId}", request.EnterpriseId);
            return new UnhandledEnterpriseException(request.EnterpriseId, ex);
        }
    }

    private async Task NotifyAdminsAsync(Enterprise enterprise, CancellationToken cancellationToken)
    {
        var memberships = await unitOfWork.UserEnterpriseMembershipRepository
            .GetActiveByUserIdsForEnterpriseAsync(enterprise.Id, cancellationToken);

        var body = EmailTemplates.EnterpriseRejectedToAdmin(
            enterprise.Name, enterprise.RejectionReason ?? "Причина не вказана.");
        foreach (var membership in memberships)
        {
            var userOption = await userManager.GetUserByIdAsync(membership.UserId);
            var email = userOption.Match(u => u.Email, () => (string?)null);
            if (string.IsNullOrWhiteSpace(email)) continue;

            await emailService.SendEmailAsync(
                toEmail: email,
                subject: $"EcoTrack: реєстрацію підприємства «{enterprise.Name}» відхилено",
                body: body,
                cancellationToken: cancellationToken);
        }
    }
}
