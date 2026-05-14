using Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.UserConfiguration;

internal class RefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.HasOne(c => c.User).WithMany(c => c.UserRefreshTokens).HasForeignKey(c => c.UserId);

        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.EnterpriseId);

        builder.HasIndex(x => new { x.UserId, x.IsValid });

        builder.ToTable("user-refresh-tokens", "usr");
    }
}