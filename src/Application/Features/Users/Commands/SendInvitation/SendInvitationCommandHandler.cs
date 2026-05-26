using System.Text.Json;
using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Auditing;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Settings;
using Application.Features.Admin.Exceptions;
using Application.Features.Users.Exceptions;
using Domain.Entities.Auditing;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.SendInvitation;

internal class SendInvitationCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IRoleManagerService roleManagerService,
    IAppUserManager userManager,
    IEmailService emailService,
    IAdminAuditService adminAudit,
    FrontendSettings frontendSettings)
    : IRequestHandler<SendInvitationCommand, Either<UserException, string>>
{
    public async Task<Either<UserException, string>> Handle(SendInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var adminEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (adminEnterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        return await CheckRoleId(request.RoleId, adminEnterpriseId.Value)
            .BindAsync(_ => HandleAsync(request, adminEnterpriseId.Value, cancellationToken));
    }

    private async Task<Either<UserException, string>> HandleAsync(SendInvitationCommand request,
        Guid adminEnterpriseId,
        CancellationToken cancellationToken)
    {
        // Existing-user invite is allowed as long as the user has no membership (active OR
        // revoked) in THIS enterprise. Revoked members go through the restore-membership flow,
        // not a fresh invite.
        var existingUserOption = await userManager.GetUserByEmail(request.Email);
        if (existingUserOption.IsSome)
        {
            var existingUser = existingUserOption.Match(u => u, () => default!);
            var existingMembership = await unitOfWork.UserEnterpriseMembershipRepository
                .GetByUserAndEnterpriseAsync(existingUser.Id, adminEnterpriseId, cancellationToken);
            if (existingMembership.IsSome)
                return new UserAlreadyHasMembershipException(existingUser.Id, adminEnterpriseId);
        }

        var existing = await unitOfWork.InvitationRepository
            .GetActiveByEnterpriseAndEmailAsync(adminEnterpriseId, request.Email, cancellationToken);
        foreach (var prior in existing)
        {
            prior.MarkAsUsed();
            unitOfWork.InvitationRepository.Update(prior);
        }

        var invitation = EnterpriseInvitation.Create(adminEnterpriseId, request.Email, request.RoleId);
        await unitOfWork.InvitationRepository.AddAsync(invitation, cancellationToken);

        var details = JsonSerializer.SerializeToDocument(new
        {
            invitationId = invitation.Id,
            email = request.Email,
            roleId = request.RoleId
        });
        await adminAudit.LogAsync(
            action: AuditAction.UserInvited,
            targetType: AuditTargetType.Invitation,
            targetId: invitation.Id,
            targetLabel: request.Email,
            enterpriseId: adminEnterpriseId,
            details: details,
            cancellationToken: cancellationToken);

        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var inviteLink = $"{frontendSettings.BaseUrl}/join?token={invitation.Token}";
            await emailService.SendEmailAsync(
                toEmail: request.Email,
                subject: "Your Invitation to EcoTrack",
                body: EmailTemplates.InvitationByEmail(inviteLink),
                cancellationToken: cancellationToken);
            // Flush the outbox row in the tx so transaction.Commit() seals it together with
            // the Invitation entity above.
            await unitOfWork.SaveChangesAsync(cancellationToken);

            transaction.Commit();
            return invitation.Token;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<Either<UserException, Domain.Entities.User.Role>> CheckRoleId(Guid roleId, Guid adminEnterpriseId)
    {
        var role = await roleManagerService.GetRoleByIdAsync(roleId);

        return role.Match<Either<UserException, Domain.Entities.User.Role>>(
            r => r.EnterpriseId == adminEnterpriseId
                ? r
                : new UserRoleNotFoundException(Guid.Empty, roleId),
            () => new UserRoleNotFoundException(Guid.Empty, roleId)
        );
    }
}