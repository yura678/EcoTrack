using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class MonitoringPlanConfiguration : IEntityTypeConfiguration<MonitoringPlan>
{
    public void Configure(EntityTypeBuilder<MonitoringPlan> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.HasOne(x => x.Installation)
            .WithMany(x => x.MonitoringPlans)
            .HasForeignKey(x => x.InstallationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Property(x => x.Version)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();
        
        builder.Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());
        
    }
}