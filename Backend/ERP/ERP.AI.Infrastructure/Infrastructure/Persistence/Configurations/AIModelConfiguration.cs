using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence.Configurations;

public class AIModelConfiguration : IEntityTypeConfiguration<AIModel>
{
    public void Configure(EntityTypeBuilder<AIModel> builder)
    {
        builder.ToTable("AIModels");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.AIProviderId)
            .IsRequired();

        builder.Property(x => x.ModelCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.SupportsChat);
        builder.Property(x => x.SupportsStructuredOutput);
        builder.Property(x => x.SupportsToolCalling);
        builder.Property(x => x.SupportsEmbeddings);
        builder.Property(x => x.SupportsTokenReporting);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        builder.Property(x => x.UpdatedByUserId);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Enforce uniqueness of the provider/model combination
        builder.HasIndex(x => new { x.AIProviderId, x.ModelCode })
            .IsUnique();

        // Navigation to AIProvider
        builder.HasOne(x => x.Provider)
            .WithMany(p => p.Models)
            .HasForeignKey(x => x.AIProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
