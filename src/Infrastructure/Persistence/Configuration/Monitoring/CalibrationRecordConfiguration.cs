using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class CalibrationRecordConfiguration : IEntityTypeConfiguration<CalibrationRecord>
{
    public void Configure(EntityTypeBuilder<CalibrationRecord> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Result)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PerformedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.NextDueAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.PerformedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CertificateNumber)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.HasOne(x => x.Device)
            .WithMany(d => d.Calibrations)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DeviceId, x.PerformedAt });
    }
}
