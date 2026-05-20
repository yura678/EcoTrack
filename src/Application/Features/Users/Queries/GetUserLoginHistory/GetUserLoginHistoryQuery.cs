using Application.Common.Models;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.GetUserLoginHistory;

public record GetUserLoginHistoryQuery(
    Guid UserId,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<Either<UserException, PageResult<LoginHistoryEntry>>>;
