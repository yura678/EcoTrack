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

    Task<Option<User>> GetUserByCode(string code);
    Task<IdentityResult> ChangePhoneNumber(User user, string phoneNumber, string code);
    public Task<IdentityResult> ChangeEmail(User user, string email, string code);

    Task<IdentityResult> VerifyUserCode(User user, string code);
    Task<string> GenerateOtpCode(User user);
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
}