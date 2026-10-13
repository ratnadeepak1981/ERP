using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using ERP.Domain.Features.CRM;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(x => x.LeadId);

        builder.Property(x => x.LeadNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CompanyName)
            .HasMaxLength(200);

        builder.Property(x => x.ContactName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

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

        builder.Property(x => x.ReferrerUrl)
            .HasMaxLength(500);

        builder.Property(x => x.DisqualificationReason)
            .HasMaxLength(500);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique lead number within company and tenant
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.LeadNumber
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
            x.LeadId
        })
        .IsUnique();

        // Query performance indices
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Status
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
            .HasForeignKey(x => new { x.TenantId, x.ConvertedCustomerId })
            .HasPrincipalKey(x => new { x.TenantId, x.CustomerId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
