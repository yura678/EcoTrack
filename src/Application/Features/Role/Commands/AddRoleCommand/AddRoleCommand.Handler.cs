using Application.Common.Interfaces.Identity;
using Application.Features.Role.Exceptions;
using Application.Models.Identity;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Role.Commands.AddRoleCommand;

internal class AddRoleCommandHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService)
    : IRequestHandler<AddRoleCommand, Either<RoleException, bool>>
{
    public async Task<Either<RoleException, bool>> Handle(
        AddRoleCommand request,
        CancellationToken cancellationToken)
    {
        return await AddRole(request);
    }

    private async Task<Either<RoleException, bool>> AddRole(
        AddRoleCommand request)
    {
        try
        {
            var enterpriseId = currentUserService.GetCurrentEnterpriseId();

            if (!enterpriseId.HasValue)
            {
                return new RoleCreationException(Guid.Empty, "Missing Enterprise context for the current user.");
            }

            var addRoleResult =
                await roleManagerService.CreateRoleAsync(new CreateRoleDto()
                    { RoleName = request.RoleName, DisplayName = request.RoleName, EnterpriseId = enterpriseId.Value });


            if (addRoleResult.Succeeded)
                return true;

            return new RoleCreationException(Guid.Empty, addRoleResult.Errors.StringifyIdentityResultErrors());
        }
        catch (Exception exception)
        {
            return new UnhandledRoleException(Guid.Empty, exception);
        }
    }
}