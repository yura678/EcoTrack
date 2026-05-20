using Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.UserConfiguration;

internal class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("login-attempts", "usr");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired(false);
        builder.Property(x => x.EmailAttempted).IsRequired().HasMaxLength(320);
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.IpAddress).IsRequired(false).HasMaxLength(64);
        builder.Property(x => x.UserAgent).IsRequired(false).HasMaxLength(512);
        builder.Property(x => x.Method).HasConversion<int>().IsRequired();
        builder.Property(x => x.Outcome).HasConversion<int>().IsRequired();

        // Per-user time-ordered scan is the hot read path (login history page); the partial
        // index keeps the index small by excluding NULL UserId rows (anonymous unknown-email
        // attempts), which are useful in aggregate but never queried per user.
        builder.HasIndex(x => new { x.UserId, x.OccurredAt })
            .HasDatabaseName("ix_login_attempts_user_id_occurred_at")
            .HasFilter("user_id IS NOT NULL");
    }
}
