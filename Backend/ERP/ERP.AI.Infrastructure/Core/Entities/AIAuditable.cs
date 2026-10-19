namespace ERP.AI.Infrastructure.Core.Entities;

public abstract class AIAuditable
{
    public DateTime CreatedAtUtc { get; protected set; }

    public Guid CreatedByUserId { get; protected set; }

    public DateTime? UpdatedAtUtc { get; protected set; }

    public Guid? UpdatedByUserId { get; protected set; }

    public void MarkCreated(Guid userId, DateTime utcNow)
    {
        CreatedAtUtc = utcNow;
        CreatedByUserId = userId;
        UpdatedAtUtc = null;
        UpdatedByUserId = null;
    }

    public void MarkUpdated(Guid userId, DateTime utcNow)
    {
        UpdatedAtUtc = utcNow;
        UpdatedByUserId = userId;
    }
}
