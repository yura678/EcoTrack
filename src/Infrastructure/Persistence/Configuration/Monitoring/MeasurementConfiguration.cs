using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WindowStart)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.WindowEnd)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.Window)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Aggregation)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Value)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.NormalizedValue)
            .HasPrecision(18, 6)
            .IsRequired(false);

        builder.Property(x => x.Uncertainty)
            .HasPrecision(18, 6)
            .IsRequired(false);

        builder.Property(x => x.ValidPointsCount).IsRequired();
        builder.Property(x => x.ExpectedPointsCount).IsRequired();
        builder.Ignore(x => x.DataAvailability);
        builder.Ignore(x => x.IsRepresentative);

        builder.Property(x => x.Quality)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.QualityNote)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.SubstitutedBy)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(x => x.SubstitutionReason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.SubstitutedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.HasIndex(x => new { x.EmissionSourceId, x.PollutantId, x.WindowEnd, x.Window, x.Aggregation })
            .IsUnique();

        builder.HasOne(x => x.EmissionSource)
            .WithMany(x => x.Measurements)
            .HasForeignKey(x => x.EmissionSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pollutant)
            .WithMany(x => x.Measurements)
            .HasForeignKey(x => x.PollutantId)
            .OnDelete(DeleteBehavior.Restrict);

        // DeviceId is nullable: materialized aggregates leave it null; manual readings keep
        // their attribution. SetNull on delete so a hard-deleted device doesn't block historical
        // manual rows — the row stays, the attribution just disappears.
        builder.HasOne(x => x.Device)
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.Measurements)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.EnterpriseId).IsRequired();
        builder.HasIndex(x => x.EnterpriseId);
    }
}
