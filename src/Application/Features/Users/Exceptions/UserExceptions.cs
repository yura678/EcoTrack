namespace Application.Features.Users.Exceptions;

public abstract class UserException(
    Guid userId,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public Guid UserId { get; } = userId;
}

public class InvalidInvitationTokenException(Guid userId)
    : UserException(userId, "Invitation token is invalid, used, or expired.");

public class UserIsLockedException(
    Guid userId,
    DateTimeOffset lockoutEnd)
    : UserException(userId, $"User is locked out. Try in {(lockoutEnd - DateTimeOffset.Now).Minutes} Minutes.");

public class UserNotFoundException(
    Guid userId)
    : UserException(userId, $"Specified user not found.");

public class EnterpriseNotFound(Guid userId)
    : UserException(userId, "Specified enterprise not found.");

public class UserRoleNotFoundException(
    Guid userId,
    Guid roleId)
    : UserException(userId, "Specified role not found.");

public class UserRoleAssignmentException(
    Guid userId,
    string roleName)
    : UserException(userId, $"Failed to assign '{roleName}' role to the new user.");

public class UserCreationException(
    Guid userId,
    string errors)
    : UserException(userId, $"Failed to create user: {errors}.");

public class UserVerificationException(
    Guid userId,
    string errors)
    : UserException(userId, $"{errors}.");

public class InvalidCredentialsException(Guid userId)
    : UserException(userId, "Invalid username or password.");

public class PhoneNumberAlreadyExistsException(
    Guid userId)
    : UserException(userId, $"Phone number already exists.");

public class UserNameAlreadyExistsException(
    Guid userId)
    : UserException(userId, "Username already exists.");

public class EmailAlreadyExistsException(
    Guid userId)
    : UserException(userId, "Email already exists.");

public class EdrpouAlreadyExistsException(string edrpou)
    : UserException(Guid.Empty, $"Enterprise with EDRPOU '{edrpou}' already exists.");

public class SectorNotFoundException(Guid sectorId)
    : UserException(Guid.Empty, "Sector was not found.")
{
    public Guid SectorId { get; } = sectorId;
}

public class InvitationEmailMismatchException()
    : UserException(Guid.Empty, "Invitation is bound to a different email address.");

public class UserAlreadyHasMembershipException(Guid userId, Guid enterpriseId)
    : UserException(userId,
        "User already has a membership (active or revoked) in this enterprise. " +
        "Use the restore-membership endpoint for revoked memberships.")
{
    public Guid EnterpriseId { get; } = enterpriseId;
}

public class InvalidRefreshTokenException(
    Guid userId)
    : UserException(userId, "Invalid refresh token.");

public class UnhandledUserException(
    Guid userId,
    Exception? innerException = null)
    : UserException(userId, "Unexpected error occurred.", innerException);

public class EnterprisePendingApprovalException(Guid userId, string edrpou)
    : UserException(userId,
        $"Enterprise (EDRPOU {edrpou}) is awaiting SuperAdmin approval. " +
        "You'll receive an email once it has been reviewed.")
{
    public string Edrpou { get; } = edrpou;
}

public class EnterpriseRejectedException(Guid userId, string edrpou, string reason)
    : UserException(userId,
        $"Enterprise registration (EDRPOU {edrpou}) was rejected: {reason}")
{
    public string Edrpou { get; } = edrpou;
    public string Reason { get; } = reason;
}