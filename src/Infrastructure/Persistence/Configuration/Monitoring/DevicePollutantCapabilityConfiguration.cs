using Domain.Entities.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class DevicePollutantCapabilityConfiguration : IEntityTypeConfiguration<DevicePollutantCapability>
{
    public void Configure(EntityTypeBuilder<DevicePollutantCapability> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RangeMin)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.RangeMax)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.AccuracyClass)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.HasOne(x => x.Device)
            .WithMany(d => d.Capabilities)
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Pollutant)
            .WithMany()
            .HasForeignKey(x => x.PollutantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RangeUnit)
            .WithMany()
            .HasForeignKey(x => x.RangeUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DeviceId, x.PollutantId })
            .IsUnique();
    }
}
