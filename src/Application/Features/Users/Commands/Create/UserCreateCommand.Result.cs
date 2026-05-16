namespace Application.Features.Users.Commands.Create;

public class UserCreateCommandResult
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool RequiresEmailConfirmation { get; set; }
}
