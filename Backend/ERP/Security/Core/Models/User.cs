namespace Security.Core.Models;
using Security.Core.Common;
public class User : Auditable
{
    public Guid Id { get; set; }

    // Null = platform-level user
    // Value = tenant-level user
    public Guid? TenantId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

   
    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();
}