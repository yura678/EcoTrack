using Domain.Entities.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class MonitoringRequirementConfiguration : IEntityTypeConfiguration<MonitoringRequirement>
{
    public void Configure(EntityTypeBuilder<MonitoringRequirement> builder)
    {
        builder.HasKey(x => x.Id);


        builder.Property(x => x.Frequency)
            .IsRequired();

        builder.HasIndex(x => new { x.MonitoringPlanId, x.EmissionSourceId, x.PollutantId })
            .IsUnique();

        builder.HasOne(x => x.MonitoringPlan)
            .WithMany(x => x.Requirements)
            .HasForeignKey(x => x.MonitoringPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.EmissionSource)
            .WithMany(x => x.MonitoringRequirements)
            .HasForeignKey(x => x.EmissionSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pollutant)
            .WithMany(x => x.MonitoringRequirements)
            .HasForeignKey(x => x.PollutantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}