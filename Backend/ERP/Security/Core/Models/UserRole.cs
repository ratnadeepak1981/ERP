namespace Security.Core.Models;
using Security.Core.Common;
public class UserRole : Auditable
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;

    public ICollection<UserRoleScope> Scopes { get; set; }
        = new List<UserRoleScope>();
}