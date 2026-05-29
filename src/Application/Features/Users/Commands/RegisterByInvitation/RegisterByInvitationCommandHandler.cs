using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Users.Commands.RegisterByInvitation;

internal class RegisterByInvitationCommandHandler(
    IUnitOfWork unitOfWork,
    IAppUserManager userManager,
    IRoleManagerService roleManagerService)
    : IRequestHandler<RegisterByInvitationCommand, Either<UserException, UserCreateCommandResult>>
{
    public async Task<Either<UserException, UserCreateCommandResult>> Handle(RegisterByInvitationCommand request,
        CancellationToken cancellationToken)
    {
        using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await HandleAsync(request, cancellationToken);
            if (result.IsLeft)
            {
                transaction.Rollback();
            }
            else
            {
                transaction.Commit();
            }

            return result;
        }
        catch (Exception exception)
        {
            transaction.Rollback();
            return new UnhandledUserException(Guid.Empty, exception);
        }
    }

    private async Task<Either<UserException, UserCreateCommandResult>>
        HandleAsync(RegisterByInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await unitOfWork.InvitationRepository.GetValidInvitation(request.Token, cancellationToken);

        return await invitation.MatchAsync<EnterpriseInvitation, Either<UserException, UserCreateCommandResult>>(
            async i =>
            {
                if (!string.Equals(i.Email, request.Email, StringComparison.OrdinalIgnoreCase))
                    return new InvitationEmailMismatchException();

                // Caller is anonymous (no JWT) — tenant query filter on Role hides the
                // invitation's target tenant role from the standard lookup. Use the
                // cross-tenant variant: the invitation token is the authorisation here.
                var roleOption = await roleManagerService.GetRoleByIdIgnoringTenantAsync(i.RoleId);
                if (roleOption.IsNone)
                    return new UserRoleNotFoundException(Guid.Empty, i.RoleId);

                var emailExist = await userManager.IsExistEmail(request.Email);
                if (emailExist)
                    return new EmailAlreadyExistsException(Guid.Empty);

                var user = new User
                {
                    UserName = request.Email,
                    Name = request.Name,
                    FamilyName = request.FamilyName,
                    Email = request.Email,
                    EmailConfirmed = true,
                };

                var createResult = await userManager.CreateUser(user, request.Password);
                if (!createResult.Succeeded)
                    return new UserCreationException(Guid.Empty, createResult.Errors.StringifyIdentityResultErrors());

                await unitOfWork.UserEnterpriseMembershipRepository.AddAsync(
                    UserEnterpriseMembership.New(Guid.NewGuid(), user.Id, i.EnterpriseId, i.RoleId),
                    cancellationToken);

                i.MarkAsUsed();
                unitOfWork.InvitationRepository.Update(i);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new UserCreateCommandResult
                {
                    UserId = user.Id,
                    Email = user.Email!,
                    RequiresEmailConfirmation = false,
                };
            },
            () => new InvalidInvitationTokenException(Guid.Empty));
    }
}