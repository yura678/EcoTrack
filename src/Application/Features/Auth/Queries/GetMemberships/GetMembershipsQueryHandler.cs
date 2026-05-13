using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries;
using Application.Models.Auth;
using MediatR;

namespace Application.Features.Auth.Queries.GetMemberships;

public class GetMembershipsQueryHandler(
    ICurrentUserService currentUserService,
    IUserEnterpriseMembershipQueries queries)
    : IRequestHandler<GetMembershipsQuery, IReadOnlyList<MembershipInfo>>
{
    public async Task<IReadOnlyList<MembershipInfo>> Handle(GetMembershipsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (!userId.HasValue) return [];

        var memberships = await queries.GetByUserIdWithRoleAndEnterpriseAsync(userId.Value, cancellationToken);

        return memberships.Select(m => new MembershipInfo(
            m.EnterpriseId,
            m.Enterprise?.Name ?? string.Empty,
            m.RoleId,
            m.Role?.Name ?? string.Empty,
            m.Role?.DisplayName,
            m.JoinedAt
        )).ToList();
    }
}
