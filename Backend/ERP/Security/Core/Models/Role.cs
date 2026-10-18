namespace Security.Core.Models;
using Security.Core.Common;
public class Role : Auditable
{
    public Guid Id { get; set; }

    // Null = platform-level role
    // Value = tenant-level role
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsSystemRole { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();
}