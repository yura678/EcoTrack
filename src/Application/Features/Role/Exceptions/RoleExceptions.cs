namespace Application.Features.Role.Exceptions;

public abstract class RoleException(
    Guid roleId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid RoleId { get; } = roleId;
}

public class RoleNotFoundException(
    Guid userId,
    Guid roleId)
    : RoleException(userId, $"Specified role with ID {roleId} not found.");
public class RoleCreationException(
    Guid roleId,
    string errors)
    : RoleException(roleId, $"Failed to create role: {errors}.");

public class RoleClaimsUpdateException(
    Guid roleId)
    : RoleException(roleId, "Failed to update role claims.");

public class UnhandledRoleException(
    Guid roleId,
    Exception? innerException = null)
    : RoleException(roleId, "Unexpected error occurred.", innerException);