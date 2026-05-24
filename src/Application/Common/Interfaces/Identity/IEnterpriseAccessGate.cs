using Application.Features.Users.Exceptions;
using LanguageExt;

namespace Application.Common.Interfaces.Identity;

/// <summary>
/// Login-time check for whether a user is allowed to receive a JWT at all. Blocks users
/// whose only tenant memberships are in enterprises that haven't been approved
/// (<see cref="Domain.Entities.Enterprises.EnterpriseStatus.Pending"/>) or were rejected
/// (<see cref="Domain.Entities.Enterprises.EnterpriseStatus.Rejected"/>) by a SuperAdmin.
/// SuperAdmin users (who have no memberships, just the global role) bypass this gate.
/// </summary>
public interface IEnterpriseAccessGate
{
    /// <summary>
    /// Returns <c>Some(exception)</c> if the user must be blocked from receiving a token,
    /// or <c>None</c> if at least one of the user's enterprises is Active (or the user has
    /// no memberships at all, e.g. SuperAdmin).
    /// </summary>
    Task<Option<UserException>> CheckLoginEligibilityAsync(
        Guid userId, CancellationToken cancellationToken);
}
