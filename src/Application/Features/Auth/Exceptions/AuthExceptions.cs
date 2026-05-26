namespace Application.Features.Auth.Exceptions;

public abstract class AuthException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class NotAuthenticatedException()
    : AuthException("User is not authenticated.");

public class MembershipNotFoundException(Guid userId, Guid enterpriseId)
    : AuthException("You don't have access to this enterprise.")
{
    public Guid UserId { get; } = userId;
    public Guid EnterpriseId { get; } = enterpriseId;
}

public class UnhandledAuthException(Exception? innerException = null)
    : AuthException("Unexpected error occurred.", innerException);
