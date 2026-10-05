namespace ERP.Domain.Common;

public abstract class Auditable
{
    public DateTime CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }
}