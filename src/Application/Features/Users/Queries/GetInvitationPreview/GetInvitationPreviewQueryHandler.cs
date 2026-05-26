using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Features.Users.Exceptions;
using Application.Models.Users;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.GetInvitationPreview;

/// <summary>
/// Resolves an invitation token into a public preview shown on the /join landing page. Returns
/// <see cref="InvalidInvitationTokenException"/> for any unknown/used/expired token — the
/// generic message is intentional so probers can't distinguish those cases.
/// </summary>
internal class GetInvitationPreviewQueryHandler(
    IUnitOfWork unitOfWork,
    IEnterpriseQueries enterpriseQueries,
    IRoleManagerService roleManagerService)
    : IRequestHandler<GetInvitationPreviewQuery, Either<UserException, InvitationPreviewInfo>>
{
    public async Task<Either<UserException, InvitationPreviewInfo>> Handle(
        GetInvitationPreviewQuery request, CancellationToken cancellationToken)
    {
        var invitation = await unitOfWork.InvitationRepository
            .GetValidInvitation(request.Token, cancellationToken);

        return await invitation.MatchAsync<EnterpriseInvitation, Either<UserException, InvitationPreviewInfo>>(
            async i =>
            {
                var enterprise = await enterpriseQueries.GetByIdAsync(i.EnterpriseId, cancellationToken);
                var enterpriseName = enterprise.Match(e => e.Name, () => string.Empty);

                var role = await roleManagerService.GetRoleByIdAsync(i.RoleId);
                var roleName = role.Match(r => r.Name ?? string.Empty, () => string.Empty);
                var roleDisplay = role.Match(r => r.DisplayName, () => null);

                return new InvitationPreviewInfo(
                    Email: i.Email,
                    EnterpriseName: enterpriseName,
                    RoleName: roleName,
                    RoleDisplayName: roleDisplay,
                    ExpiresAt: i.ExpiresAt);
            },
            () => new InvalidInvitationTokenException(Guid.Empty));
    }
}
