namespace SaaS.Core.Models;
using SaaS.Common;
public class TenantDatabase : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string DatabaseName { get; set; } = string.Empty;

    public string DatabaseServer { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
}