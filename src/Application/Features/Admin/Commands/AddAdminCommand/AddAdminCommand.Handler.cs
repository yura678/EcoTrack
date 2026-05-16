using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Admin.Exceptions;
using Domain.Entities.User;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Admin.Commands.AddAdminCommand;

internal class AddAdminCommandHandler(
    IAppUserManager userManager,
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddAdminCommand, Either<AdminException, bool>>
{
    public async Task<Either<AdminException, bool>> Handle(
        AddAdminCommand request,
        CancellationToken cancellationToken)
    {
        return await CheckRoleId(request.RoleId)
            .BindAsync(role => AddAdmin(request, role, cancellationToken));
    }

    private async Task<Either<AdminException, bool>> AddAdmin(
        AddAdminCommand request,
        Domain.Entities.User.Role role,
        CancellationToken cancellationToken)
    {
        if (role.EnterpriseId is null)
            return new RoleNotFoundException(Guid.Empty, role.Id);

        var newAdmin = new User
        {
            UserName = request.UserName,
            Email = request.Email
        };

        try
        {
            var createResult = await userManager.CreateUserWithPasswordAsync(newAdmin, request.Password);
            if (!createResult.Succeeded)
                return new AdminCreationException(Guid.Empty, createResult.Errors.StringifyIdentityResultErrors());

            await unitOfWork.UserEnterpriseMembershipRepository.AddAsync(
                UserEnterpriseMembership.New(Guid.NewGuid(), newAdmin.Id, role.EnterpriseId.Value, role.Id),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception e)
        {
            return new UnhandledAdminException(Guid.Empty, e);
        }
    }

    private async Task<Either<AdminException, Domain.Entities.User.Role>> CheckRoleId(Guid roleId)
    {
        var role = await roleManagerService.GetRoleByIdAsync(roleId);

        return await role.MatchAsync<Domain.Entities.User.Role, Either<AdminException, Domain.Entities.User.Role>>(
            Some: r =>
            {
                if (currentUserService.IsSuperAdmin())
                    return Task.FromResult<Either<AdminException, Domain.Entities.User.Role>>(r);

                var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
                if (currentEnterpriseId is null || r.EnterpriseId != currentEnterpriseId)
                    return Task.FromResult<Either<AdminException, Domain.Entities.User.Role>>(
                        new RoleNotFoundException(Guid.Empty, roleId));

                return Task.FromResult<Either<AdminException, Domain.Entities.User.Role>>(r);
            },
            None: () => new RoleNotFoundException(Guid.Empty, roleId)
        );
    }
}
