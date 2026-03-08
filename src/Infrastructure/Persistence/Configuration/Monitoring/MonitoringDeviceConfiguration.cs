using Domain.Entities.EmissionSources;
using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class MonitoringDeviceConfiguration : IEntityTypeConfiguration<MonitoringDevice>
{
    public void Configure(EntityTypeBuilder<MonitoringDevice> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.SerialNumber)
            .IsUnique();

        builder.Property(x => x.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsOnline)
            .IsRequired();

        builder.Property(x => x.InstalledAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.Property(x => x.Notes)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.EmissionSourceId)
            .IsRequired(false);
        
        builder.HasOne(x => x.EmissionSource)
            .WithMany(e => e.MonitoringDevices)
            .HasForeignKey(x => x.EmissionSourceId)
            .OnDelete(DeleteBehavior.SetNull);
        
        builder.HasOne(x => x.Installation)
            .WithMany()
            .HasForeignKey(x => x.InstallationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}