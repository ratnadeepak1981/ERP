namespace Security.Infrastructure.Persistence.Auditing;

public class SecurityAuditRecord
{
    public Guid AuditId { get; set; }

    public Guid? TenantId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public DateTime Timestamp { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }
}