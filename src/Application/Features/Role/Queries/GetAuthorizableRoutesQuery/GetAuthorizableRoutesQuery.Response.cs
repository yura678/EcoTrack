namespace Application.Features.Role.Queries.GetAuthorizableRoutesQuery;

public record GetAuthorizableRoutesQueryResponse(
    string RouteKey,
    string? AreaName,
    string? ControllerName,
    string? ActionName,
    string? ControllerDescription);