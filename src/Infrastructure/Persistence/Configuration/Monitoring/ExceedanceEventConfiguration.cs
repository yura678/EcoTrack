using Domain.Entities.Enterprises;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class ExceedanceEventConfiguration : IEntityTypeConfiguration<ExceedanceEvent>
{
    public void Configure(EntityTypeBuilder<ExceedanceEvent> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Magnitude)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DetectedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.HasOne(x => x.Measurement)
            .WithMany()
            .HasForeignKey(x => x.MeasurementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Limit)
            .WithMany()
            .HasForeignKey(x => x.LimitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}