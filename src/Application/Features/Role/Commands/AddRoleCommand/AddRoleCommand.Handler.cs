using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Persistence;
using Application.Features.Role.Exceptions;
using Application.Models.Identity;
using LanguageExt;
using MediatR;
using Shared.Extensions;

namespace Application.Features.Role.Commands.AddRoleCommand;

internal class AddRoleCommandHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddRoleCommand, Either<RoleException, bool>>
{
    public async Task<Either<RoleException, bool>> Handle(
        AddRoleCommand request,
        CancellationToken cancellationToken)
    {
        return await AddRole(request, cancellationToken);
    }

    private async Task<Either<RoleException, bool>> AddRole(
        AddRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var enterpriseId = currentUserService.GetCurrentEnterpriseId();

            if (!enterpriseId.HasValue)
            {
                return new RoleCreationException(Guid.Empty, "Missing Enterprise context for the current user.");
            }

            var (addRoleResult, _) =
                await roleManagerService.CreateRoleAsync(new CreateRoleDto()
                    { RoleName = request.Name, DisplayName = request.Name, EnterpriseId = enterpriseId.Value });

            if (!addRoleResult.Succeeded)
            {
                return new RoleCreationException(Guid.Empty, addRoleResult.Errors.StringifyIdentityResultErrors());
            }

            // RoleManagerService.CreateRoleAsync only stages the new role in the change tracker
            // (RoleStore.AutoSaveChanges is false). Without this flush the role evaporates when
            // the request scope disposes and the API returns a misleading 200 OK to the caller.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            return new UnhandledRoleException(Guid.Empty, exception);
        }
    }
}