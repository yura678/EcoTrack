using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Enterprises;

public class EmissionLimitConfiguration : IEntityTypeConfiguration<EmissionLimit>
{
    public void Configure(EntityTypeBuilder<EmissionLimit> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.LimitType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Period)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ValidFrom)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter());

        builder.Property(x => x.ValidTo)
            .IsRequired(false)
            .HasConversion(new DateTimeUtcConverter());

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.EmissionLimits)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pollutant)
            .WithMany(x => x.EmissionLimits)
            .HasForeignKey(x => x.PollutantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EmissionSource)
            .WithMany(x => x.EmissionLimits)
            .HasForeignKey(x => x.EmissionSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Installation)
            .WithMany()
            .HasForeignKey(x => x.InstallationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Permit)
            .WithMany(x => x.EmissionLimits)
            .HasForeignKey(x => x.PermitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
