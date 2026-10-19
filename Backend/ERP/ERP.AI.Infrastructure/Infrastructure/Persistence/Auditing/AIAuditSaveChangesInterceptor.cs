using System.Text.Json;
using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Security.Interfaces;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence.Auditing;

public class AIAuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly Guid SystemUserId = Guid.Empty;
    private readonly ICurrentUserContext? _currentUserContext;

    public AIAuditSaveChangesInterceptor(ICurrentUserContext? currentUserContext = null)
    {
        _currentUserContext = currentUserContext;
    }

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

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext context)
    {
        var now = DateTime.UtcNow;
        Guid actorUserId = ResolveActorUserId();
        Guid? tenantId = ResolveTenantId();

        var auditableEntries = context.ChangeTracker
            .Entries<AIAuditable>()
            .Where(x =>
                x.State == EntityState.Added ||
                x.State == EntityState.Modified ||
                x.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in auditableEntries)
        {
            ApplyAuditFields(entry, actorUserId, now);
        }

        AddAuditRecords(context, auditableEntries, actorUserId, tenantId, now);
    }

    private Guid ResolveActorUserId()
    {
        try
        {
            if (_currentUserContext != null)
            {
                return _currentUserContext.UserId;
            }
        }
        catch
        {
            // Fallback to SystemUserId if no active HTTP user context exists
        }

        return SystemUserId;
    }

    private Guid? ResolveTenantId()
    {
        try
        {
            if (_currentUserContext != null)
            {
                return _currentUserContext.TenantId;
            }
        }
        catch
        {
            // Null when no tenant context
        }

        return null;
    }

    private static void ApplyAuditFields(
        EntityEntry<AIAuditable> entry,
        Guid actorUserId,
        DateTime now)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.MarkCreated(actorUserId, now);
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Entity.MarkUpdated(actorUserId, now);
        }
    }

    private static void AddAuditRecords(
        DbContext context,
        List<EntityEntry<AIAuditable>> entries,
        Guid actorUserId,
        Guid? tenantId,
        DateTime now)
    {
        foreach (var entry in entries)
        {
            // Try extracting TenantId from entity if it has one (e.g. TenantAIConfiguration)
            Guid? entryTenantId = tenantId;
            var tenantProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
            if (tenantProp?.CurrentValue is Guid g)
            {
                entryTenantId = g;
            }

            var auditRecord = new AIAuditRecord
            {
                Id = Guid.NewGuid(),
                TenantId = entryTenantId,
                ActorUserId = actorUserId == Guid.Empty ? null : actorUserId,
                EventType = GetEventType(entry.State),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = GetEntityId(entry),
                OccurredAtUtc = now,
                Outcome = "Success",
                SafeMetadataJson = GetSafeMetadataJson(entry)
            };

            context.Set<AIAuditRecord>().Add(auditRecord);
        }
    }

    private static string GetEventType(EntityState state)
    {
        return state switch
        {
            EntityState.Added => "EntityCreated",
            EntityState.Modified => "EntityUpdated",
            EntityState.Deleted => "EntityDeleted",
            _ => "Unknown"
        };
    }

    private static string GetEntityId(EntityEntry<AIAuditable> entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return string.Empty;

        var keyValues = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);

        return string.Join(";", keyValues);
    }

    private static string? GetSafeMetadataJson(EntityEntry<AIAuditable> entry)
    {
        try
        {
            var changes = new Dictionary<string, object?>();
            foreach (var prop in entry.Properties)
            {
                // Exclude concurrency tokens or raw keys if any
                if (prop.Metadata.IsConcurrencyToken) continue;
                if (prop.Metadata.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                    prop.Metadata.Name.Contains("Key", StringComparison.OrdinalIgnoreCase) && !prop.Metadata.Name.EndsWith("Id"))
                {
                    continue;
                }

                if (entry.State == EntityState.Added)
                {
                    changes[prop.Metadata.Name] = prop.CurrentValue;
                }
                else if (entry.State == EntityState.Modified && prop.IsModified)
                {
                    changes[prop.Metadata.Name] = new
                    {
                        Old = prop.OriginalValue,
                        New = prop.CurrentValue
                    };
                }
            }

            return changes.Count > 0 ? JsonSerializer.Serialize(changes) : null;
        }
        catch
        {
            return null;
        }
    }
}
