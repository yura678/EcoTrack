using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Monitoring;

public class MeasureUnitConfiguration : IEntityTypeConfiguration<MeasureUnit>
{
    public void Configure(EntityTypeBuilder<MeasureUnit> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Symbol)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(p => p.Symbol)
            .IsUnique();
        
        builder.Property(x => x.Dimension)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ToBaseFactor)
            .IsRequired();
        
        builder.Property(x => x.CreatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();
    }
}