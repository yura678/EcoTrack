using Application.Features.Users.Exceptions;
using Application.Models.Users;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.GetUserDetail;

public record GetUserDetailQuery(Guid UserId) : IRequest<Either<UserException, UserDetailInfo>>;
