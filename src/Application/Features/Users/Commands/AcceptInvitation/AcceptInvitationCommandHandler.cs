using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.AcceptInvitation;

/// <summary>
/// Authenticated path for an existing user to accept an invitation to a new enterprise.
/// Creates a <see cref="UserEnterpriseMembership"/> for the JWT-identified user without any
/// password flow — registration is the separate <c>RegisterByInvitation</c> command for users
/// who don't have an account yet. Email of the JWT subject must match the invitation's email.
/// </summary>
internal class AcceptInvitationCommandHandler(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IAppUserManager userManager)
    : IRequestHandler<AcceptInvitationCommand, Either<UserException, AcceptInvitationResult>>
{
    public async Task<Either<UserException, AcceptInvitationResult>> Handle(
        AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await HandleAsync(request, cancellationToken);
            if (result.IsLeft)
                transaction.Rollback();
            else
                transaction.Commit();
            return result;
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return new UnhandledUserException(Guid.Empty, exception);
        }
    }

    private async Task<Either<UserException, AcceptInvitationResult>> HandleAsync(
        AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        // [Authorize] on the endpoint already keeps anonymous out, but defend against a
        // misconfigured pipeline by returning a 404-shaped UserNotFound when the JWT is empty.
        var userId = currentUserService.GetCurrentUserId();
        if (!userId.HasValue)
            return new UserNotFoundException(Guid.Empty);

        var userOption = await userManager.GetUserByIdAsync(userId.Value);
        var user = userOption.Match<User?>(u => u, () => null);
        if (user is null)
            return new UserNotFoundException(userId.Value);

        var invitationOption = await unitOfWork.InvitationRepository
            .GetValidInvitation(request.Token, cancellationToken);

        return await invitationOption.MatchAsync<EnterpriseInvitation, Either<UserException, AcceptInvitationResult>>(
            async invitation =>
            {
                if (!string.Equals(invitation.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                    return new InvitationEmailMismatchException();

                var existingMembership = await unitOfWork.UserEnterpriseMembershipRepository
                    .GetByUserAndEnterpriseAsync(user.Id, invitation.EnterpriseId, cancellationToken);
                if (existingMembership.IsSome)
                    return new UserAlreadyHasMembershipException(user.Id, invitation.EnterpriseId);

                var membership = UserEnterpriseMembership.New(
                    Guid.NewGuid(), user.Id, invitation.EnterpriseId, invitation.RoleId);
                await unitOfWork.UserEnterpriseMembershipRepository.AddAsync(membership, cancellationToken);

                invitation.MarkAsUsed();
                unitOfWork.InvitationRepository.Update(invitation);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new AcceptInvitationResult(invitation.EnterpriseId, membership.Id);
            },
            () => new InvalidInvitationTokenException(Guid.Empty));
    }
}
