using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Common.Settings;
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
    AdminSettings adminSettings,
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
                transaction.Rollback();
            else
                transaction.Commit();

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
            return new UserNameAlreadyExistsException(Guid.Empty);

        var emailExist = await userManager.IsExistEmail(request.Email);
        if (emailExist)
            return new EmailAlreadyExistsException(Guid.Empty);

        var sectorOption = await unitOfWork.SectorRepository.GetByIdAsync(request.SectorId, cancellationToken);
        if (sectorOption.IsNone)
            return new SectorNotFoundException(request.SectorId);

        var edrpouTaken = await unitOfWork.EnterpriseRepository
            .GetByEdrpouIncludingDeletedAsync(request.Edrpou, cancellationToken);
        if (edrpouTaken.IsSome)
            return new EdrpouAlreadyExistsException(request.Edrpou);

        var enterpriseId = Guid.NewGuid();
        // Self-service registrations land as Pending — a SuperAdmin must verify the EDRPOU
        // against the official EDR before any user under this tenant can log in. The login
        // gate enforces this; we just have to mark the row correctly here.
        var enterprise = Enterprise.NewPending(
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
            Email = request.Email,
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

        var (createResultRole, createdRoleId) = await roleManager.CreateRoleAsync(new CreateRoleDto
            { RoleName = role.Name, DisplayName = role.DisplayName, EnterpriseId = role.EnterpriseId });

        if (!createResultRole.Succeeded || createdRoleId is null)
        {
            return new UserRoleAssignmentException(Guid.Empty, "admin");
        }

        // Commit the new User + Role before seeding permissions. SeedAllDynamicPermissionsAsync
        // queries the role from the database; with AutoSaveChanges=false on the role store, the
        // role would otherwise still live only in the change tracker and the query would return
        // null — leaving the brand-new admin without a single DynamicPermission claim.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await roleManager.SeedAllDynamicPermissionsAsync(createdRoleId.Value);

        await unitOfWork.UserEnterpriseMembershipRepository.AddAsync(
            UserEnterpriseMembership.New(Guid.NewGuid(), user.Id, enterpriseId, createdRoleId.Value),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var code = await userManager.GenerateEmailConfirmationCodeAsync(user);

        logger.LogInformation("Issued email confirmation code for enterprise admin {UserId}", user.Id);

        var emailBody = EmailTemplates.EmailConfirmation(code);

        await emailService.SendEmailAsync(
            toEmail: request.Email,
            subject: "EcoTrack",
            body: emailBody,
            cancellationToken: cancellationToken
        );

        // Notify SuperAdmin that a new tenant is awaiting approval. Empty NotificationEmail
        // (default in dev/test) disables this so the outbox stays clean during local runs.
        if (!string.IsNullOrWhiteSpace(adminSettings.NotificationEmail))
        {
            var adminBody = EmailTemplates.NewEnterpriseRegistrationToSuperAdmin(
                request.EnterpriseName, request.Edrpou, request.Email);
            await emailService.SendEmailAsync(
                toEmail: adminSettings.NotificationEmail,
                subject: $"EcoTrack: нова заявка від {request.EnterpriseName} ({request.Edrpou})",
                body: adminBody,
                cancellationToken: cancellationToken
            );
        }

        // Flush the email_outbox row alongside the rest of the registration in the same
        // explicit transaction; transaction.Commit() further up wouldn't flush tracked-but-
        // unsaved entities, and the IEmailService.SendEmailAsync is intentionally save-free.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserCreateCommandResult
        {
            UserId = user.Id,
            Email = user.Email!,
            RequiresEmailConfirmation = true,
        };
    }
}