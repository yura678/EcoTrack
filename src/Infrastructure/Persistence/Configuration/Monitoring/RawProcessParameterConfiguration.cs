using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class RawProcessParameterConfiguration : IEntityTypeConfiguration<RawProcessParameter>
{
    public void Configure(EntityTypeBuilder<RawProcessParameter> builder)
    {
        // Composite PK (Id, Time) — required by TimescaleDB hypertables:
        // every unique index must include the partitioning column.
        builder.HasKey(x => new { x.Id, x.Time });
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();

        builder.Property(x => x.Time)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired();

        builder.Property(x => x.IngestionTime)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.ParameterType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Value)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.Quality)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => new { x.EmissionSourceId, x.ParameterType, x.Time });
        builder.HasIndex(x => x.Time);
    }
}
