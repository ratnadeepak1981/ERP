using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using ERP.Domain.Features.CRM;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class CrmActivityConfiguration : IEntityTypeConfiguration<CrmActivity>
{
    public void Configure(EntityTypeBuilder<CrmActivity> builder)
    {
        builder.ToTable("CrmActivities");

        builder.HasKey(x => x.CrmActivityId);

        builder.Property(x => x.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.CompletionNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.IsCompleted)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Branch and company index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        // Associated entity indices
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.LeadId
        });

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.OpportunityId
        });

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId
        });

        // Query performance indices
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.IsCompleted,
            x.DueDate
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

        builder.HasOne<Lead>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LeadId })
            .HasPrincipalKey(x => new { x.TenantId, x.LeadId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Opportunity>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OpportunityId })
            .HasPrincipalKey(x => new { x.TenantId, x.OpportunityId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CustomerId })
            .HasPrincipalKey(x => new { x.TenantId, x.CustomerId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
