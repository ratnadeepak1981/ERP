namespace Security.Core.Models;
using Security.Core.Common;
public class Permission : Auditable
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    // Permission ownership: Platform permissions are never assignable by tenant administrators.
    public string Category { get; set; } = PermissionCategory.Tenant;

    // The business operation represented by the permission (VIEW, CREATE, EDIT, APPROVE, POST, ...).
    public string Operation { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();
}