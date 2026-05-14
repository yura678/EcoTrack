using Domain.Entities.EmissionSources;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.EmissionSources;

public class EmissionSourceConfiguration : IEntityTypeConfiguration<EmissionSource>
{
    public void Configure(EntityTypeBuilder<EmissionSource> builder)
    {
        builder.UseTptMappingStrategy();
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(e => new { e.InstallationId, e.Code })
            .IsUnique();

        builder.Property(x => x.Location)
            .HasColumnType("geometry(Point, 4326)")
            .IsRequired();

        builder.HasIndex(x => x.Location)
            .HasMethod("gist");

        builder.Property(x => x.CreatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.Property(x => x.DeletedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.HasOne(x => x.Installation)
            .WithMany(x => x.EmissionSources)
            .HasForeignKey(x => x.InstallationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.EnterpriseId).IsRequired();
        builder.HasIndex(x => x.EnterpriseId);
    }
}