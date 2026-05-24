using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration.Enterprises;

public class EnterpriseConfiguration : IEntityTypeConfiguration<Enterprise>
{
    public void Configure(EntityTypeBuilder<Enterprise> builder)
    {
        builder.HasKey(x => x.Id);


        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Edrpou)
            .IsRequired()
            .HasMaxLength(12);
        
        builder.HasIndex(x => x.Edrpou)
            .IsUnique();

        builder.Property(x => x.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.RiskGroup)
            .IsRequired()
            .HasConversion<int>();

        // EnterpriseStatus persists as int; the column's database-side default (1=Active) is
        // set inside the AddEnterpriseApprovalStatus migration solely to backfill rows that
        // pre-date the approval gate. HasDefaultValue is deliberately NOT used on the model
        // because Pending=0 collides with the CLR default for the enum — EF Core treats "value
        // equals CLR default" as "user didn't set it, use DB default", which would silently
        // promote every freshly registered Pending tenant to Active. By dropping the model-side
        // default we force EF to write the explicit Status value on every INSERT.
        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ApprovalDecisionAt)
            .HasConversion(new DateTimeUtcConverter())
            .IsRequired(false);

        builder.Property(x => x.ApprovalDecisionByUserId)
            .IsRequired(false);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(2000)
            .IsRequired(false);

        // SuperAdmin's pending list filters by Status; tiny working set so a partial index is
        // overkill, but the regular composite index also covers operator queries like
        // "approvals decided this week".
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_enterprise_status_created");

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

        builder.HasOne(x => x.Sector)
            .WithMany(s => s.Enterprises)
            .HasForeignKey(x => x.SectorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}