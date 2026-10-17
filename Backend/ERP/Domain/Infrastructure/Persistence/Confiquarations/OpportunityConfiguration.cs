using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using ERP.Domain.Features.CRM;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("Opportunities");

        builder.HasKey(x => x.OpportunityId);

        builder.Property(x => x.OpportunityNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ProbabilityPercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.EstimatedValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.CampaignSource)
            .HasMaxLength(100);

        builder.Property(x => x.CampaignMedium)
            .HasMaxLength(50);

        builder.Property(x => x.CampaignCode)
            .HasMaxLength(50);

        builder.Property(x => x.LossReason)
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique opportunity number within company and tenant
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.OpportunityNumber
        })
        .IsUnique();

        // Branch-level filter index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        // Tenant-aware composite index for child entity reference
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.OpportunityId
        })
        .IsUnique();

        // Query performance indices
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId
        });

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Stage
        });

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.AssignedToUserId
        });

        // Foreign keys
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CompanyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CustomerId })
            .HasPrincipalKey(x => new { x.TenantId, x.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lead>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LeadId })
            .HasPrincipalKey(x => new { x.TenantId, x.LeadId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
