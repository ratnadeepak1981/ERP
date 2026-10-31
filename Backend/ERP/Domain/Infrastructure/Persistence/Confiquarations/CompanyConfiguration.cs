using Domain.Features.MasterData.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.Code)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.SpecialUomConversionEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.BaseCurrencyId)
            .IsRequired(false);

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Id
        })
        .IsUnique();

        builder.HasOne(x => x.BaseCurrency)
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.BaseCurrencyId })
            .HasPrincipalKey(x => new { x.TenantId, x.CurrencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
