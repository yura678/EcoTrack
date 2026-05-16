using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RevokeMembership;

internal class RevokeMembershipCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    ICurrentUserService currentUserService)
    : IRequestHandler<RevokeMembershipCommand, Either<UserException, bool>>
{
    public async Task<Either<UserException, bool>> Handle(RevokeMembershipCommand request,
        CancellationToken cancellationToken)
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        if (enterpriseId is null)
            return new EnterpriseNotFound(Guid.Empty);

        if (request.UserId == currentUserService.GetCurrentUserId())
            return new UserVerificationException(request.UserId,
                "Admin cannot revoke their own membership; transfer admin rights first.");

        var membershipOption = await unitOfWork.UserEnterpriseMembershipRepository
            .GetByUserAndEnterpriseAsync(request.UserId, enterpriseId.Value, cancellationToken);
        var membership = membershipOption.Match(m => m, () => null!);
        if (membership is null || !membership.IsActive)
            return new UserNotFoundException(request.UserId);

        membership.Revoke();
        unitOfWork.UserEnterpriseMembershipRepository.Update(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var userOption = await userManager.GetUserByIdAsync(request.UserId);
        await userOption.MatchAsync<User, bool>(
            Some: async u => { await userManager.UpdateSecurityStampAsync(u); return true; },
            None: () => false);

        return true;
    }
}
