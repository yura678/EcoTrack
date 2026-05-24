using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries;
using Application.Features.Users.Exceptions;
using Domain.Entities.Enterprises;
using LanguageExt;

namespace Infrastructure.Identity;

/// <summary>
/// Loads the user's enterprise memberships (with Enterprise included) and decides whether
/// at least one is in <see cref="EnterpriseStatus.Active"/>. If none are Active, picks the
/// most informative exception to return — Rejected wins over Pending so the user sees the
/// specific rejection reason instead of "still pending".
/// </summary>
internal class EnterpriseAccessGate(
    IUserEnterpriseMembershipQueries membershipQueries)
    : IEnterpriseAccessGate
{
    public async Task<Option<UserException>> CheckLoginEligibilityAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var memberships = await membershipQueries
            .GetByUserIdWithRoleAndEnterpriseAsync(userId, cancellationToken);

        // No memberships → SuperAdmin or fresh-invite-not-yet-redeemed. Either way this gate
        // isn't the right place to block — JwtService and downstream policies handle access.
        if (memberships.Count == 0)
            return Option<UserException>.None;

        if (memberships.Any(m => m.Enterprise?.Status == EnterpriseStatus.Active))
            return Option<UserException>.None;

        // Prefer Rejected — gives the user a concrete reason to act on instead of a vague
        // "still pending" message they would otherwise just keep waiting on.
        var rejected = memberships
            .Where(m => m.Enterprise?.Status == EnterpriseStatus.Rejected)
            .Select(m => m.Enterprise!)
            .FirstOrDefault();
        if (rejected is not null)
        {
            return new EnterpriseRejectedException(
                userId, rejected.Edrpou,
                rejected.RejectionReason ?? "Причина не вказана.");
        }

        var pending = memberships
            .Where(m => m.Enterprise?.Status == EnterpriseStatus.Pending)
            .Select(m => m.Enterprise!)
            .FirstOrDefault();
        if (pending is not null)
        {
            return new EnterprisePendingApprovalException(userId, pending.Edrpou);
        }

        // Suspended (or unrecognised state) — treat as pending-style block. Use the
        // pending exception to surface the EDRPOU so an operator can investigate.
        var suspended = memberships
            .Where(m => m.Enterprise is not null)
            .Select(m => m.Enterprise!)
            .First();
        return new EnterprisePendingApprovalException(userId, suspended.Edrpou);
    }
}
