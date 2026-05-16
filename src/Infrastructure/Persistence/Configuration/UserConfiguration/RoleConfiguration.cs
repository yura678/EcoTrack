using Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.UserConfiguration;

internal class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "usr");

        builder.HasOne(r => r.Enterprise)
            .WithMany()
            .HasForeignKey(r => r.EnterpriseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}