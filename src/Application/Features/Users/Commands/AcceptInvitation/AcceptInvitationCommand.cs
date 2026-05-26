using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.AcceptInvitation;

public record AcceptInvitationCommand(string Token)
    : IRequest<Either<UserException, AcceptInvitationResult>>;

public record AcceptInvitationResult(Guid EnterpriseId, Guid MembershipId);
