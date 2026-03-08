namespace Application.Features.Admin.Exceptions;

public abstract class AdminException(
    Guid userId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid UserId { get; } = userId;
}

public class RoleNotFoundException(
    Guid userId,
    Guid roleId)
    : AdminException(userId, $"Specified role with ID {roleId} not found.");

public class UserNotFoundException(
    Guid userId,
    string userName)
    : AdminException(userId, $"Specified user with Username {userName} not found.");

public class UserHasNoRolesException(Guid userId)
    : AdminException(userId, "This user does not have any role assigned.");

public class InvalidCredentialsException(Guid userId)
    : AdminException(userId, "Invalid password.");

public class UserIsLockedException(
    Guid userId,
    DateTimeOffset lockoutEnd)
    : AdminException(userId, $"User is locked out. Try in {(lockoutEnd - DateTimeOffset.Now).Minutes} Minutes.");

public class AdminCreationException(
    Guid userId,
    string errors)
    : AdminException(userId, $"Failed to create admin: {errors}.");

public class UnhandledAdminException(
    Guid userId,
    Exception? innerException = null)
    : AdminException(userId, "Unexpected error occurred.", innerException);