using Domain.Entities.EmissionSources;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.EmissionSources;

public class PollutantConfiguration : IEntityTypeConfiguration<Pollutant>
{
    public void Configure(EntityTypeBuilder<Pollutant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.Property(x => x.CasNumber)
            .HasMaxLength(20)
            .IsRequired(false);

        builder.HasIndex(x => x.CasNumber)
            .IsUnique()
            .HasFilter("cas_number IS NOT NULL");

        builder.Property(x => x.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Media)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DefaultDimension)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CanonicalUnitId)
            .IsRequired();

        builder.HasOne(x => x.CanonicalUnit)
            .WithMany()
            .HasForeignKey(x => x.CanonicalUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.MolarMass)
            .HasPrecision(10, 4)
            .IsRequired(false);

        builder.Property(x => x.DefaultO2Reference)
            .HasPrecision(5, 2)
            .IsRequired(false);

        builder.Property(x => x.EprtrThresholdKgYear)
            .HasPrecision(18, 4)
            .IsRequired(false);

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
    }
}
