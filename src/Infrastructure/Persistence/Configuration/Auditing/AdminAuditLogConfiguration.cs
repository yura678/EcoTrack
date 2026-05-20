using Domain.Entities.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Auditing;

internal class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("admin-audit-log", "audit");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EnterpriseId).IsRequired(false);
        builder.Property(x => x.OccurredAt).IsRequired();

        builder.Property(x => x.ActorUserId).IsRequired();
        builder.Property(x => x.ActorEmail).IsRequired().HasMaxLength(320);
        builder.Property(x => x.ActorRole).IsRequired(false).HasMaxLength(128);

        builder.Property(x => x.Action).HasConversion<int>().IsRequired();
        builder.Property(x => x.TargetType).HasConversion<int>().IsRequired();
        builder.Property(x => x.TargetId).IsRequired(false);
        builder.Property(x => x.TargetLabel).IsRequired(false).HasMaxLength(512);

        // JsonDocument → jsonb. Per-row action-specific payload that doesn't deserve its own
        // column. Querying by key is still possible with PG operators when needed.
        builder.Property(x => x.Details)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(x => x.IpAddress).IsRequired(false).HasMaxLength(64);
        builder.Property(x => x.UserAgent).IsRequired(false).HasMaxLength(512);

        // Lookups: most reads are "give me events for this enterprise, newest first" and
        // "give me events for this target". Two indices cover both shapes.
        builder.HasIndex(x => new { x.EnterpriseId, x.OccurredAt })
            .HasDatabaseName("ix_admin_audit_log_enterprise_occurred_at");
        builder.HasIndex(x => new { x.TargetType, x.TargetId })
            .HasDatabaseName("ix_admin_audit_log_target");
    }
}
