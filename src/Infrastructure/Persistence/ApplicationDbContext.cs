using Application.Common.Interfaces.Identity;
using Domain.Common;
using Domain.Entities.User;
using Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;

namespace Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions options,
    ICurrentUserService currentUserService)
    : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<DateTimeUtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<Role>().Metadata.RemoveIndex(
            modelBuilder.Entity<Role>().Property(r => r.NormalizedName).Metadata.GetContainingIndexes().Single()
        );

        modelBuilder.Entity<Role>()
            .HasIndex(r => new { r.NormalizedName, r.EnterpriseId })
            .HasDatabaseName("RoleNameIndex")
            .IsUnique()
            .AreNullsDistinct(false);


        var entitiesAssembly = typeof(IEntity).Assembly;
        modelBuilder.RegisterAllEntities<IEntity>(entitiesAssembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}