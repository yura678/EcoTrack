using Domain.Entities.Notifications;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Notifications;

public class NotificationSubscriptionConfiguration : IEntityTypeConfiguration<NotificationSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationSubscription> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(320) // RFC 5321 maximum
            .IsRequired(false);

        builder.Property(x => x.WebhookUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(x => x.WebhookSecret)
            .HasMaxLength(256)
            .IsRequired(false);

        // ComplianceEventType[] stored as int[] PG array via per-element conversion.
        builder.Property(x => x.EventTypes)
            .HasConversion(
                v => v == null ? null : v.Select(e => (int)e).ToArray(),
                v => v == null ? null : v.Select(i => (Domain.Entities.Monitoring.ComplianceEventType)i).ToArray())
            .HasColumnType("integer[]")
            .IsRequired(false);

        builder.Property(x => x.EmissionSourceIds)
            .HasColumnType("uuid[]")
            .IsRequired(false);

        builder.Property(x => x.IsEnabled).IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.EnterpriseId });
        builder.Property(x => x.EnterpriseId).IsRequired();
        builder.HasIndex(x => x.EnterpriseId);
    }
}
