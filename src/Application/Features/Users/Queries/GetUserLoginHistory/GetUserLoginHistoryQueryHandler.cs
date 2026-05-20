using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Interfaces.Repositories;
using Application.Common.Models;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.GetUserLoginHistory;

/// <summary>
/// Admin view of another user's login history. Same tenant-scoping rule as user detail:
/// admin must have an active membership relationship with the target user in their enterprise
/// before reading anything. SuperAdmin bypasses.
/// </summary>
internal class GetUserLoginHistoryQueryHandler(
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    ILoginAttemptRepository repository)
    : IRequestHandler<GetUserLoginHistoryQuery, Either<UserException, PageResult<LoginHistoryEntry>>>
{
    public async Task<Either<UserException, PageResult<LoginHistoryEntry>>> Handle(
        GetUserLoginHistoryQuery request, CancellationToken cancellationToken)
    {
        var isSuperAdmin = currentUserService.IsSuperAdmin();
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (!isSuperAdmin && enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        if (!isSuperAdmin)
        {
            var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
                .GetByUserAndEnterpriseAsync(request.UserId, enterpriseId!.Value, cancellationToken);
            var isMember = membershipOption.Match(
                Some: m => m.IsActive,
                None: () => false);
            if (!isMember) return new UserNotFoundException(request.UserId);
        }

        var page = await repository.GetForUserPagedAsync(
            request.UserId, request.From, request.To,
            request.Page, request.PageSize, cancellationToken);

        return new PageResult<LoginHistoryEntry>
        {
            Items = page.Items.Select(a => new LoginHistoryEntry(
                a.Id, a.OccurredAt, a.Method, a.Outcome, a.IpAddress, a.UserAgent)).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }
}
