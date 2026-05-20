using Application.Common.Models;
using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMyLoginHistory;

public record GetMyLoginHistoryQuery(
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize) : IRequest<Either<UserException, PageResult<LoginHistoryEntry>>>;
