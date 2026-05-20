using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Repositories;
using Application.Common.Models;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMyLoginHistory;

internal class GetMyLoginHistoryQueryHandler(
    ICurrentUserService currentUserService,
    ILoginAttemptRepository repository)
    : IRequestHandler<GetMyLoginHistoryQuery, Either<UserException, PageResult<LoginHistoryEntry>>>
{
    public async Task<Either<UserException, PageResult<LoginHistoryEntry>>> Handle(
        GetMyLoginHistoryQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        if (userId is null) return new UserNotFoundException(Guid.Empty);

        var page = await repository.GetForUserPagedAsync(
            userId.Value, request.From, request.To,
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
