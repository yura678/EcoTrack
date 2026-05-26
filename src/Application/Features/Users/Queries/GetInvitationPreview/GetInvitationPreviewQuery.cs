using Application.Features.Users.Exceptions;
using Application.Models.Users;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Queries.GetInvitationPreview;

public record GetInvitationPreviewQuery(string Token)
    : IRequest<Either<UserException, InvitationPreviewInfo>>;
