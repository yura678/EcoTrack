using Domain.Entities.EmissionSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.EmissionSources;

internal class AirEmissionConfiguration : IEntityTypeConfiguration<AirEmissionSource>
{
    public void Configure(EntityTypeBuilder<AirEmissionSource> builder)
    {
        
        builder.Property(x => x.Height)
            .IsRequired();
        
        builder.Property(x => x.Diameter)
            .IsRequired();
        
        builder.Property(x => x.DesignFlowRate)
            .IsRequired();
        
    }
}