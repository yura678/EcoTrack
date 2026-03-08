using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Enterprises;

public class PermitConfiguration : IEntityTypeConfiguration<Permit>
{
    public void Configure(EntityTypeBuilder<Permit> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.HasIndex(x => x.Number)
            .IsUnique();

        builder.Property(x => x.PermitType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IssuedAt)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter());
        
        builder.Property(x => x.ValidUntil)
            .IsRequired()
            .HasConversion(new DateTimeUtcConverter());
        
        builder.Property(x => x.Authority)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);
        
        builder.Property(x => x.CreatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .HasDefaultValueSql("timezone('utc', now())")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);
        
        builder.HasOne(x => x.Installation)
            .WithMany()
            .HasForeignKey(x => x.InstallationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
        
    }
}