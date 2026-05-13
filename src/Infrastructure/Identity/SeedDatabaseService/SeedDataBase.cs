using Domain.Entities.User;
using Infrastructure.Identity.Manager;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity.SeedDatabaseService;

public interface ISeedDataBase
{
    Task Seed();
}

public class SeedDataBase : ISeedDataBase
{
    private readonly AppUserManager _userManager;
    private readonly AppRoleManager _roleManager;
    private readonly ApplicationDbContext _db;

    public SeedDataBase(
        AppUserManager userManager,
        AppRoleManager roleManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    public async Task Seed()
    {
        if (!_roleManager.Roles.AsNoTracking().Any(r => r.Name.Equals("superAdmin")))
        {
            var role = new Role
            {
                Name = "superAdmin",
                DisplayName = "Super Admin",
                EnterpriseId = null
            };
            await _roleManager.CreateAsync(role);
        }

        if (!_userManager.Users.AsNoTracking().Any(u => u.UserName.Equals("superAdmin")))
        {
            var user = new User
            {
                UserName = "superAdmin",
                Email = "superAdmin@site.com",
                PhoneNumberConfirmed = true,
            };

            await _userManager.CreateAsync(user, "qw123321");
            await _userManager.AddToRoleAsync(user, "superAdmin");
        }
    }
}
