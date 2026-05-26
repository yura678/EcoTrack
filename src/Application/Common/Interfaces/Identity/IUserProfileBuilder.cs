using Application.Models.Profile;
using Domain.Entities.User;

namespace Application.Common.Interfaces.Identity;

/// <summary>
/// Projects a <see cref="MyProfileInfo"/> for a given user and enterprise context. Used by
/// the GetMyProfile query AND by every token-issuing endpoint that bundles the profile into
/// its response. Keeps the projection logic in one place so JWT claims and profile payload
/// cannot diverge.
/// </summary>
public interface IUserProfileBuilder
{
    Task<MyProfileInfo> BuildAsync(User user, Guid? activeEnterpriseId, CancellationToken cancellationToken);
}
