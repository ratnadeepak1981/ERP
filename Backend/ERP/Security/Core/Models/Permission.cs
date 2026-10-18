namespace Security.Core.Models;
using Security.Core.Common;
public class Permission : Auditable
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();
}