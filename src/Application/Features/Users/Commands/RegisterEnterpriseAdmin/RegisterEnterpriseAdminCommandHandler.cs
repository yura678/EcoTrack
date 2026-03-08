using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Commands.Create;
using Application.Features.Users.Exceptions;
using Application.Models.Identity;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Extensions;

namespace Application.Features.Users.Commands.RegisterEnterpriseAdmin;

internal class RegisterEnterpriseAdminCommandHandler(
    IAppUserManager userManager,
    IRoleManagerService roleManager,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    ILogger<RegisterEnterpriseAdminCommandHandler> logger)
    : IRequestHandler<RegisterEnterpriseAdminCommand, Either<UserException, UserCreateCommandResult>>
{
    public async Task<Either<UserException, UserCreateCommandResult>> Handle(
        RegisterEnterpriseAdminCommand request,
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
            logger.LogError(exception, "Error registering enterprise and admin.");
            return new UnhandledUserException(Guid.Empty, exception);
        }
    }

    private async Task<Either<UserException, UserCreateCommandResult>> HandleAsync(
        RegisterEnterpriseAdminCommand request,
        CancellationToken cancellationToken)
    {
        var userNameExist = await userManager.IsExistUserName(request.UserName);
        if (userNameExist)
            return new PhoneNumberAlreadyExistsException(Guid.Empty);

        var phoneNumberExist = await userManager.IsExistUser(request.PhoneNumber);
        if (phoneNumberExist)
            return new PhoneNumberAlreadyExistsException(Guid.Empty);

        var emailNumberExist = await userManager.IsExistEmail(request.Email);
        if (emailNumberExist)
            return new EmailAlreadyExistsException(Guid.Empty);

        var enterpriseId = Guid.NewGuid();
        var enterprise = Enterprise.New(
            id: enterpriseId,
            name: request.EnterpriseName,
            edrpou: request.Edrpou,
            address: request.Address,
            riskGroup: RiskGroup.None,
            sectorId: request.SectorId
        );

        await unitOfWork.EnterpriseRepository.AddAsync(enterprise, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var user = new User
        {
            UserName = request.UserName,
            Name = request.Name,
            FamilyName = request.FamilyName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            EnterpriseId = enterpriseId
        };

        var createResult = await userManager.CreateUser(user, request.Password);

        if (!createResult.Succeeded)
        {
            return new UserCreationException(Guid.Empty, createResult.Errors.StringifyIdentityResultErrors());
        }

        var role = new Domain.Entities.User.Role
        {
            Name = "admin",
            DisplayName = "Admin",
            EnterpriseId = enterpriseId
        };

        await roleManager.CreateRoleAsync(new CreateRoleDto
            { RoleName = role.Name, DisplayName = role.DisplayName, EnterpriseId = role.EnterpriseId });
        
        var roleResult = await userManager.AddUserToRoleAsync(user, role);

        if (!roleResult.Succeeded)
        {
            return new UserRoleAssignmentException(Guid.Empty, "admin");
        }

        var code = await userManager.GenerateEmailConfirmationToken(user, user.PhoneNumber);

        logger.LogWarning($"Generated Code for User ID {user.Id} is {code}");

        var emailBody = EmailTemplates.EmailConfirmation(code);

        await emailService.SendEmailAsync(
            toEmail: request.Email,
            subject: "EcoTrack",
            body: emailBody,
            cancellationToken: cancellationToken
        );

        return new UserCreateCommandResult { UserGeneratedKey = user.GeneratedCode };
    }
}