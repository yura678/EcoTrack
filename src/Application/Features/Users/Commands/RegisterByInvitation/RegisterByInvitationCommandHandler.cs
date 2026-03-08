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
    IAppUserManager userManager)
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
                var userNameExist = await userManager.IsExistUserName(request.UserName);
                if (userNameExist)
                    return new PhoneNumberAlreadyExistsException(Guid.Empty);

                var phoneNumberExist = await userManager.IsExistUser(request.PhoneNumber);
                if (phoneNumberExist)
                    return new UserNameAlreadyExistsException(Guid.Empty);

                var emailNumberExist = await userManager.IsExistEmail(request.Email);
                if (emailNumberExist)
                    return new EmailAlreadyExistsException(Guid.Empty);
                
                var user = new User
                {
                    UserName = request.UserName,
                    Name = request.Name,
                    FamilyName = request.FamilyName,
                    PhoneNumber = request.PhoneNumber,
                    Email = request.Email,
                    EmailConfirmed = true,
                    EnterpriseId = i.EnterpriseId
                };

                var createResult = await userManager.CreateUser(user, request.Password);
                if (!createResult.Succeeded)
                    return new UserCreationException(Guid.Empty, createResult.Errors.StringifyIdentityResultErrors());
                i.MarkAsUsed();
                unitOfWork.InvitationRepository.Update(i);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return new UserCreateCommandResult { UserGeneratedKey = user.GeneratedCode };
            },
            () => new InvalidInvitationTokenException(Guid.Empty));
    }
}