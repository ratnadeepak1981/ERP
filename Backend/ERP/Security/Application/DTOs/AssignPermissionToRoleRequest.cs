namespace Security.Application.DTOs;

public class AssignPermissionToRoleRequest
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }
}