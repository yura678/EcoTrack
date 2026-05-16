using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Admin.Exceptions;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.SendInvitation;

internal class SendInvitationCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IRoleManagerService roleManagerService,
    IEmailService emailService)
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
        var invitation = EnterpriseInvitation.Create(adminEnterpriseId, request.Email, request.RoleId);

        await unitOfWork.InvitationRepository.AddAsync(invitation, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var inviteLink = $"https://ecotrack.com/join?token={invitation.Token}";

        var emailBody = EmailTemplates.InvitationByEmail(inviteLink);

        await emailService.SendEmailAsync(
            toEmail: request.Email,
            subject: "Your Invitation to EcoTrack",
            body: emailBody,
            cancellationToken: cancellationToken
        );

        return invitation.Token;
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