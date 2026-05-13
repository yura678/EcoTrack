namespace Application.Features.Auth.Exceptions;

public abstract class AuthException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class NotAuthenticatedException()
    : AuthException("User is not authenticated.");

public class MembershipNotFoundException(Guid userId, Guid enterpriseId)
    : AuthException(
        $"Active membership for user '{userId}' in enterprise '{enterpriseId}' was not found.");

public class UnhandledAuthException(Exception? innerException = null)
    : AuthException("Unexpected error occurred.", innerException);
