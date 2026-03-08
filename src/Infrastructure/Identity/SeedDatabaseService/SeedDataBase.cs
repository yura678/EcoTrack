using Domain.Entities.User;
using Domain.Entities.Enterprises;
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
        var systemEnterpriseId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var systemSectorId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var systemEnterprise = await _db.Set<Enterprise>()
            .FirstOrDefaultAsync(e => e.Id == systemEnterpriseId);

        if (systemEnterprise is null)
        {
            systemEnterprise = Enterprise.New(
                id: systemEnterpriseId,
                name: "EcoTrack System Administration",
                edrpou: "00000000", 
                address: "System Address",
                riskGroup: RiskGroup.None,
                sectorId: systemSectorId
            );

            _db.Set<Enterprise>().Add(systemEnterprise);
            
            var systemSector = await _db.Set<Sector>().FindAsync(systemSectorId);
            if (systemSector is null)
            {
                _db.Set<Sector>().Add(Sector.New(systemSectorId, "System Sector", ""));
            }

            await _db.SaveChangesAsync();
        }

        if (!_roleManager.Roles.AsNoTracking().Any(r => r.Name.Equals("superAdmin")))
        {
            var role = new Role
            {
                Name = "superAdmin",
                DisplayName = "Super Admin",
                EnterpriseId = systemEnterpriseId
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
                EnterpriseId = systemEnterpriseId
            };

            await _userManager.CreateAsync(user, "qw123321");
            await _userManager.AddToRoleAsync(user, "superAdmin");
        }
    }
}