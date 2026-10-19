namespace Security.Core.Models;
using Security.Core.Common;
public class UserRoleScope : Auditable
{
    public Guid Id { get; set; }

    public Guid UserRoleId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    public UserRole UserRole { get; set; } = null!;
}