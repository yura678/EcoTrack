using MediatR;

namespace Application.Features.Role.Queries.GetAllRolesQuery;

public record GetAllEnterpriseRolesQuery() : IRequest<List<GetAllEnterpriseRolesQueryResponse>>;