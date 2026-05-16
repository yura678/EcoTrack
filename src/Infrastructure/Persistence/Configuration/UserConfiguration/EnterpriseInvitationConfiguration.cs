using Domain.Entities.Enterprises;
using Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.UserConfiguration;

internal class EnterpriseInvitationConfiguration : IEntityTypeConfiguration<EnterpriseInvitation>
{
    public void Configure(EntityTypeBuilder<EnterpriseInvitation> builder)
    {
        builder.ToTable("enterprise-invitations", "usr");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Token).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.IsUsed).IsRequired();

        builder.HasOne(x => x.Enterprise)
            .WithMany(e => e!.Invitations)
            .HasForeignKey(x => x.EnterpriseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.EnterpriseId, x.Email });
    }
}
