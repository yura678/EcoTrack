using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class RawMeasurementConfiguration : IEntityTypeConfiguration<RawMeasurement>
{
    public void Configure(EntityTypeBuilder<RawMeasurement> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.Time)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.IngestionTime)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.RawValue)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.Quality)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => new { x.EmissionSourceId, x.PollutantId, x.Time });
        builder.HasIndex(x => x.Time);
    }
}
