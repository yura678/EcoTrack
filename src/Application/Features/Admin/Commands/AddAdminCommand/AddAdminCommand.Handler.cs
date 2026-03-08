using Application.Common.Interfaces.Identity;
using Application.Features.Admin.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Admin.Commands.AddAdminCommand;

internal class AddAdminCommandHandler(
    IAppUserManager userManager,
    IRoleManagerService roleManagerService)
    : IRequestHandler<AddAdminCommand, Either<AdminException, bool>>
{
    public async Task<Either<AdminException, bool>> Handle(
        AddAdminCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckRoleId(request.RoleId)
            .BindAsync(role => AddAdmin(request, role));
    }

    private async Task<Either<AdminException, bool>> AddAdmin(
        AddAdminCommand request,
        Domain.Entities.User.Role role)
    {
        var newAdmin = new User
        {
            UserName = request.UserName,
            Email = request.Email
        };
        try
        {
            var adminCreateResult = await userManager.CreateUserWithPasswordAsync(newAdmin, request.Password);

            if (!adminCreateResult.Succeeded)
                return new AdminCreationException(Guid.Empty, adminCreateResult.Errors.StringifyIdentityResultErrors());

            var addAdminToRoleResult = await userManager.AddUserToRoleAsync(newAdmin, role);

            if (addAdminToRoleResult.Succeeded)
                return true;

            return new AdminCreationException(Guid.Empty, addAdminToRoleResult.Errors.StringifyIdentityResultErrors());
        }
        catch (Exception e)
        {
            return new UnhandledAdminException(Guid.Empty, e);
        }
    }

    private async Task<Either<AdminException, Domain.Entities.User.Role>> CheckRoleId(Guid roleId)
    {
        var role = await roleManagerService.GetRoleByIdAsync(roleId);

        return role.Match<Either<AdminException, Domain.Entities.User.Role>>(
            r => r,
            () => new RoleNotFoundException(Guid.Empty, roleId)
        );
    }
}