using Application.Features.Users.Exceptions;
using Application.Models.Profile;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Queries.GetMyProfile;

public record GetMyProfileQuery : IRequest<Either<UserException, MyProfileInfo>>;
