using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMySessions;

public record GetMySessionsQuery : IRequest<Either<UserException, IReadOnlyList<SessionInfo>>>;
