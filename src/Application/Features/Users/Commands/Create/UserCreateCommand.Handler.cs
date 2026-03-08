using Application.Common.EmailTemplates;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Users.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Extensions;

namespace Application.Features.Users.Commands.Create;

internal class UserCreateCommandHandler(
    IAppUserManager userManager,
    IEmailService emailService,
    ILogger<UserCreateCommandHandler> logger)
    : IRequestHandler<UserCreateCommand, Either<UserException, UserCreateCommandResult>>
{
    public async Task<Either<UserException, UserCreateCommandResult>> Handle(UserCreateCommand request,
        CancellationToken cancellationToken)
    {
        var phoneNumberExist = await userManager.IsExistUser(request.PhoneNumber);

        if (phoneNumberExist)
            return new PhoneNumberAlreadyExistsException(Guid.Empty);

        var emailExist = await userManager.IsExistEmail(request.Email);

        if (emailExist)
            return new EmailAlreadyExistsException(Guid.Empty);

        var userNameExist = await userManager.IsExistUserName(request.UserName);

        if (userNameExist)
            return new UserNameAlreadyExistsException(Guid.Empty);

        var user = new User
        {
            UserName = request.UserName,
            Name = request.Name,
            FamilyName = request.FamilyName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        var createResult = string.IsNullOrEmpty(request.Password)
            ? await userManager.CreateUser(user)
            : await userManager.CreateUser(user, request.Password);

        if (!createResult.Succeeded)
        {
            return new UserCreationException(Guid.Empty, createResult.Errors.StringifyIdentityResultErrors());
        }

        var code = await userManager.GenerateEmailConfirmationToken(user, user.Email);


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