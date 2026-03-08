
namespace Application.Features.Users.Queries.GetUsers;

public record GetUsersQueryResponse 
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public Guid UserId { get; set; }
}