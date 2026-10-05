using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SaaS.Common;

namespace SaaS.Infrastructure.Persistence.Auditing;

public class PlatformAuditSaveChangesInterceptor
    : SaveChangesInterceptor
{
    private static readonly Guid SystemUserId = Guid.Empty;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            ApplyAudit(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            ApplyAudit(eventData.Context);
        }

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private static void ApplyAudit(DbContext context)
    {
        var now = DateTime.UtcNow;

        var entries = context.ChangeTracker
            .Entries<Auditable>()
            .Where(x =>
                x.State == EntityState.Added ||
                x.State == EntityState.Modified ||
                x.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            ApplyAuditFields(entry, now);
        }

        AddAuditRecords(context, entries, now);
    }

    private static void ApplyAuditFields(
        EntityEntry<Auditable> entry,
        DateTime now)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property(x => x.CreatedAt)
                .CurrentValue = now;

            entry.Property(x => x.CreatedBy)
                .CurrentValue = SystemUserId;

            entry.Property(x => x.ModifiedAt)
                .CurrentValue = null;

            entry.Property(x => x.ModifiedBy)
                .CurrentValue = null;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property(x => x.CreatedAt)
                .IsModified = false;

            entry.Property(x => x.CreatedBy)
                .IsModified = false;

            entry.Property(x => x.ModifiedAt)
                .CurrentValue = now;

            entry.Property(x => x.ModifiedBy)
                .CurrentValue = SystemUserId;
        }
    }

    private static void AddAuditRecords(
        DbContext context,
        List<EntityEntry<Auditable>> entries,
        DateTime now)
    {
        foreach (var entry in entries)
        {
            var auditRecord = new PlatformAuditRecord
            {
                AuditId = Guid.NewGuid(),
                TenantId = GetTenantId(entry),
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = GetEntityId(entry),
                Action = GetAction(entry.State),
                UserId = SystemUserId,
                Timestamp = now,
                OldValues = GetOldValues(entry),
                NewValues = GetNewValues(entry)
            };

            context.Set<PlatformAuditRecord>()
                .Add(auditRecord);
        }
    }

    private static string GetAction(EntityState state)
    {
        return state switch
        {
            EntityState.Added => "Created",
            EntityState.Modified => "Modified",
            EntityState.Deleted => "Deleted",
            _ => "Unknown"
        };
    }

    private static string GetEntityId(
        EntityEntry<Auditable> entry)
    {
        var key = entry.Metadata.FindPrimaryKey();

        if (key is null)
        {
            return string.Empty;
        }

        var values = key.Properties
            .Select(property =>
                entry.Property(property.Name).CurrentValue?.ToString()
                ?? string.Empty);

        return string.Join("|", values);
    }

    private static Guid? GetTenantId(
        EntityEntry<Auditable> entry)
    {
        var tenantProperty =
            entry.Metadata.FindProperty("TenantId");

        if (tenantProperty is null)
        {
            return null;
        }

        var value =
            entry.Property("TenantId").CurrentValue;

        return value is Guid tenantId
            ? tenantId
            : null;
    }

    private static string? GetOldValues(
        EntityEntry<Auditable> entry)
    {
        if (entry.State == EntityState.Added)
        {
            return null;
        }

        var values = entry.Properties
            .Where(x =>
                x.IsModified ||
                entry.State == EntityState.Deleted)
            .ToDictionary(
                x => x.Metadata.Name,
                x => x.OriginalValue);

        return JsonSerializer.Serialize(values);
    }

    private static string? GetNewValues(
        EntityEntry<Auditable> entry)
    {
        if (entry.State == EntityState.Deleted)
        {
            return null;
        }

        var values = entry.Properties
            .Where(x =>
                x.IsModified ||
                entry.State == EntityState.Added)
            .ToDictionary(
                x => x.Metadata.Name,
                x => x.CurrentValue);

        return JsonSerializer.Serialize(values);
    }
}