using Application.Models.Jwt;

namespace Application.Features.Admin.Queries.GetToken;

public record AdminGetTokenQueryResult(AccessToken Token, string[] Roles);