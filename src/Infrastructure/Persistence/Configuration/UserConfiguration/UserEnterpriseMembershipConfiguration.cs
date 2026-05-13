using Domain.Entities.User;
using Infrastructure.Persistence.Converters;    
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.UserConfiguration;

public class UserEnterpriseMembershipConfiguration : IEntityTypeConfiguration<UserEnterpriseMembership>
{
    public void Configure(EntityTypeBuilder<UserEnterpriseMembership> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JoinedAt)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.RevokedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Enterprise)
            .WithMany(e => e.Memberships)
            .HasForeignKey(x => x.EnterpriseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.EnterpriseId })
            .IsUnique();
    }
}
