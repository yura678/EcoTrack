using Application.Common.Interfaces.Identity;
using Application.Features.Role.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Role.Commands.DeleteRole;

internal class DeleteRoleCommandHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteRoleCommand, Either<RoleException, bool>>
{
    public async Task<Either<RoleException, bool>> Handle(DeleteRoleCommand request,
        CancellationToken cancellationToken)
    {
        var roleOption = await roleManagerService.GetRoleByIdAsync(request.RoleId);
        var caller = currentUserService;
        var authorized = roleOption.Match(
            Some: r => caller.IsSuperAdmin()
                       || (caller.GetCurrentEnterpriseId() is Guid eid && r.EnterpriseId == eid),
            None: () => false);
        if (!authorized)
            return new RoleNotFoundException(Guid.Empty, request.RoleId);

        var deleted = await roleManagerService.DeleteRoleAsync(request.RoleId);
        return deleted
            ? true
            : new UnhandledRoleException(request.RoleId);
    }
}
