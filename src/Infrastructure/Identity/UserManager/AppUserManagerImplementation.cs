using Application.Common.Interfaces.Identity;
using Domain.Entities.User;
using Infrastructure.Identity.Dtos;
using Infrastructure.Identity.Manager;
using LanguageExt;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity.UserManager;

public class AppUserManagerImplementation(AppUserManager userManager, ICurrentUserService currentUserService) : IAppUserManager
{
    public Task<IdentityResult> CreateUser(User user)
    {
        return userManager.CreateAsync(user);
    }

    public async Task<IdentityResult> CreateUser(User user, string password)
    {
        return await userManager.CreateAsync(user, password);
    }

    public Task<bool> IsExistUser(string phoneNumber)
    {
        return userManager.Users.AnyAsync(c => c.PhoneNumber == phoneNumber);
    }

    public async Task<bool> IsExistEmail(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user != null;
    }

    public async Task<bool> IsExistUserName(string userName)
    {
        var user = await userManager.FindByNameAsync(userName);
        return user != null;
    }

    public async Task<string> GeneratePhoneNumberConfirmationToken(User user, string phoneNumber)
    {
        return await userManager.GenerateChangePhoneNumberTokenAsync(user, phoneNumber);
    }

    public async Task<string> GenerateEmailConfirmationToken(User user, string email)
    {
        return await userManager.GenerateChangeEmailTokenAsync(user, email);
    }
    public async Task<Option<User>> GetUserByCode(string code)
    {
        return await userManager.Users.FirstOrDefaultAsync(c => c.GeneratedCode.Equals(code));
    }

    public Task<IdentityResult> ChangePhoneNumber(User user, string phoneNumber, string code)
    {
        return userManager.ChangePhoneNumberAsync(user, phoneNumber, code);
    }
    public Task<IdentityResult> ChangeEmail(User user, string email, string code)
    {
        return userManager.ChangeEmailAsync(user, email, code);
    }

    public async Task<IdentityResult> VerifyUserCode(User user, string code)
    {
        var confirmationResult = await userManager.VerifyUserTokenAsync(
            user, CustomIdentityConstants.OtpPasswordLessLoginProvider,
            CustomIdentityConstants.OtpPasswordLessLoginPurpose, code);

        if (confirmationResult)
            await userManager.UpdateSecurityStampAsync(user);

        return confirmationResult
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError() { Description = "Incorrect Code" });
    }

    public Task<string> GenerateOtpCode(User user)
    {
        return userManager.GenerateUserTokenAsync(
            user, CustomIdentityConstants.OtpPasswordLessLoginProvider,
            CustomIdentityConstants.OtpPasswordLessLoginPurpose);
    }

    public async Task<Option<User>> GetUserByPhoneNumber(string phoneNumber)
    {
        return await userManager.Users.FirstOrDefaultAsync(c => c.PhoneNumber.Equals(phoneNumber));
    }

    public async Task<Option<User>> GetUserByEmail(string email)
    {
        return await userManager.FindByEmailAsync(email);
    }


    public async Task<Option<User>> GetByUserName(string userName)
    {
        return await userManager.FindByNameAsync(userName);
    }

    public async Task<Option<User>> GetUserByIdAsync(Guid userId)
    {
        return await userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await userManager.Users.AsNoTracking().ToListAsync();
    }
    
    public async Task<List<User>> GetAllEnterpriseUsersAsync()
    {
        var enterpriseId = currentUserService.GetCurrentEnterpriseId();
        return await userManager.Users.AsNoTracking().Where(x=> x.EnterpriseId == enterpriseId).ToListAsync();
    }

    public async Task<IdentityResult> CreateUserWithPasswordAsync(User user, string password)
    {
        return await userManager.CreateAsync(user, password);
    }

    public async Task<IdentityResult> AddUserToRoleAsync(User user, Role role)
    {
        ArgumentNullException.ThrowIfNull(role, nameof(role));
        ArgumentNullException.ThrowIfNull(role.Name, nameof(role.Name));

        return await userManager.AddToRoleAsync(user, role.Name);
    }

    public async Task<IdentityResult> IncrementAccessFailedCountAsync(User user)
    {
        return await userManager.AccessFailedAsync(user);
    }

    public async Task<bool> IsUserLockedOutAsync(User user)
    {
        var lockoutEndDate = await userManager.GetLockoutEndDateAsync(user);

        return (lockoutEndDate.HasValue && lockoutEndDate.Value > DateTimeOffset.UtcNow);
    }

    public async Task ResetUserLockoutAsync(User user)
    {
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
    }

    public async Task UpdateUserAsync(User user)
    {
        await userManager.UpdateAsync(user);
    }

    public async Task UpdateSecurityStampAsync(User user)
    {
        await userManager.UpdateSecurityStampAsync(user);
    }

    public async Task<bool> IsPasswordValidAsync(User user, string password)
    {
        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task<string[]> GetRoleAsync(User user)
    {
        return (await userManager.GetRolesAsync(user)).ToArray();
    }
}