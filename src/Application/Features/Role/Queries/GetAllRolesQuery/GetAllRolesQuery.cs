using Application.Models.Common;
using MediatR;

namespace Application.Features.Role.Queries.GetAllRolesQuery;

public record GetAllRolesQuery() : IRequest<List<GetAllRolesQueryResponse>>;