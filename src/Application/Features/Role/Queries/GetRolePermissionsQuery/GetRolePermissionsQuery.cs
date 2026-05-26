using Application.Features.Role.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Role.Queries.GetRolePermissionsQuery;

public record GetRolePermissionsQuery(Guid RoleId)
    : IRequest<Either<RoleException, IReadOnlyList<string>>>;
