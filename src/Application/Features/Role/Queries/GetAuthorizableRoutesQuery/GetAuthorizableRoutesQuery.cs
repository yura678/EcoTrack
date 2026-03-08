using MediatR;

namespace Application.Features.Role.Queries.GetAuthorizableRoutesQuery;

public record GetAuthorizableRoutesQuery() : IRequest<List<GetAuthorizableRoutesQueryResponse>>;