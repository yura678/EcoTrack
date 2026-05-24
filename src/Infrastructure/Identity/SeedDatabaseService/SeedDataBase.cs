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
            // AppRoleStore is AutoSaveChanges = false; the next step (AddToRoleAsync) looks the
            // role up by normalized name on a fresh query, so it must be committed first.
            await _db.SaveChangesAsync();
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
            // Flush so AddToRoleAsync sees the user persisted; AppUserStore is AutoSaveChanges
            // = false now, so CreateAsync only stages.
            await _db.SaveChangesAsync();
            await _userManager.AddToRoleAsync(user, "superAdmin");
            await _db.SaveChangesAsync();
        }
    }
}
