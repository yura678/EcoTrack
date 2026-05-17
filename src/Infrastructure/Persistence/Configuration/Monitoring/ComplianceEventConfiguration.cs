using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class ComplianceEventConfiguration : IEntityTypeConfiguration<ComplianceEvent>
{
    public void Configure(EntityTypeBuilder<ComplianceEvent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Ratio)
            .HasPrecision(18, 4)
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.WindowStart)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.WindowEnd)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.DetectedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.ClosedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.ResolutionReason)
            .HasConversion<int?>()
            .IsRequired(false);

        builder.Property(x => x.ResolutionNote)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.ResolvedByUserId)
            .IsRequired(false);

        builder.HasOne(x => x.EmissionSource)
            .WithMany()
            .HasForeignKey(x => x.EmissionSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Measurement)
            .WithMany()
            .HasForeignKey(x => x.MeasurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Limit)
            .WithMany()
            .HasForeignKey(x => x.LimitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.EmissionSourceId, x.DetectedAt });

        builder.Property(x => x.EnterpriseId).IsRequired();
        builder.HasIndex(x => x.EnterpriseId);
    }
}
