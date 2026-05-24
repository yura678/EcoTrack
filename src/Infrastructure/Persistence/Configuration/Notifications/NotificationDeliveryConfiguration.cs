using Domain.Entities.Notifications;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Notifications;

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionId).IsRequired();
        builder.Property(x => x.ComplianceEventId).IsRequired();
        builder.Property(x => x.EnterpriseId).IsRequired();

        builder.Property(x => x.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.AttemptCount).IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.FirstAttemptedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.Property(x => x.LastAttemptedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.Property(x => x.DeliveredAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        // Idempotency key — the per-sub dispatcher uses (subscriptionId, complianceEventId) as
        // the natural lookup, and a unique constraint prevents duplicate attempts on Hangfire
        // re-run from racing into two inserts.
        builder.HasIndex(x => new { x.SubscriptionId, x.ComplianceEventId })
            .IsUnique()
            .HasDatabaseName("ix_notification_delivery_sub_event");

        // Operator audit query: "show me deliveries still stuck Pending" — partial index keeps
        // the hot working set tiny once most rows transition to Delivered.
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_notification_delivery_pending")
            .HasFilter("status = 0");

        builder.HasIndex(x => x.EnterpriseId)
            .HasDatabaseName("ix_notification_delivery_enterprise");
    }
}
