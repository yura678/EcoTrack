using Domain.Entities.User;
using LanguageExt;
using Microsoft.AspNetCore.Identity;

namespace Application.Common.Interfaces.Identity;

public interface IAppUserManager
{
    Task<IdentityResult> CreateUser(User user);
    Task<IdentityResult> CreateUser(User user, string password);
    Task<bool> IsExistUser(string phoneNumber);
    Task<bool> IsExistUserName(string userName);
    Task<bool> IsExistEmail(string userName);

    Task<string> GeneratePhoneNumberConfirmationToken(User user, string phoneNumber);
    Task<string> GenerateEmailConfirmationToken(User user, string email);

    Task<IdentityResult> ChangePhoneNumber(User user, string phoneNumber, string code);
    public Task<IdentityResult> ChangeEmail(User user, string email, string code);

    Task<IdentityResult> VerifyUserCode(User user, string code);
    Task<string> GenerateOtpCode(User user);

    Task<string> GenerateEmailConfirmationCodeAsync(User user);
    Task<IdentityResult> ConfirmEmailWithCodeAsync(User user, string code);
    Task<Option<User>> GetUserByPhoneNumber(string phoneNumber);
    Task<Option<User>> GetUserByEmail(string email);

    Task<Option<User>> GetByUserName(string userName);

    Task<Option<User>> GetUserByIdAsync(Guid userId);
    Task<List<User>> GetAllUsersAsync();
    Task<List<User>> GetAllEnterpriseUsersAsync();
    Task<IdentityResult> CreateUserWithPasswordAsync(User user, string password);
    Task<IdentityResult> AddUserToRoleAsync(User user, Role role);
    Task<IdentityResult> IncrementAccessFailedCountAsync(User user);
    Task<bool> IsUserLockedOutAsync(User user);
    Task ResetUserLockoutAsync(User user);
    Task UpdateUserAsync(User user);
    Task UpdateSecurityStampAsync(User user);

    Task<bool> IsPasswordValidAsync(User user, string password);
    Task<string[]> GetRoleAsync(User user);

    /// <summary>
    /// Generates an ASP.NET Identity stateless reset token for the user. Token is signed +
    /// time-bound and embeds the user's security stamp — rotating the stamp invalidates it.
    /// </summary>
    Task<string> GeneratePasswordResetTokenAsync(User user);

    /// <summary>
    /// Consumes a reset token and sets a new password. On success the user's security stamp
    /// is rotated automatically by the underlying UserManager, which invalidates the token
    /// and any other reset link in flight for the same user.
    /// </summary>
    Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword);
}