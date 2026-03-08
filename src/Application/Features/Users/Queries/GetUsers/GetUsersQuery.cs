using Application.Models.Common;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers;

public record GetUsersQuery : IRequest<List<GetUsersQueryResponse>>;


public record GetEnterpriseUsersQuery : IRequest<List<GetUsersQueryResponse>>;