using Domain.Entities.EmissionSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.EmissionSources;

public class WaterEmissionConfiguration : IEntityTypeConfiguration<WaterEmissionSource>
{
    public void Configure(EntityTypeBuilder<WaterEmissionSource> builder)
    {
        
        builder.Property(x => x.Receiver)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.Property(x => x.DesignFlowRate)
            .IsRequired();
    }
}