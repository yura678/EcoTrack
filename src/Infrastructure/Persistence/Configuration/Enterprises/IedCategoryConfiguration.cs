using Domain.Entities.Enterprises;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Enterprises;

public class IedCategoryConfiguration : IEntityTypeConfiguration<IedCategory>
{
    public void Configure(EntityTypeBuilder<IedCategory> builder)
    {
        builder.HasKey(x => x.Id);


        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Description)
            .IsRequired(false)
            .HasMaxLength(1000);
    }
}