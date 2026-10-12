namespace ERP.AI.Infrastructure.Core.Entities;

public class AIAuditRecord
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Outcome { get; set; } = "Success"; // "Success", "Failure", "Denied"

    public string? SafeMetadataJson { get; set; }
}
