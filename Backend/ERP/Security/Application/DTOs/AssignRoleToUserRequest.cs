namespace Security.Application.DTOs;

public class AssignRoleToUserRequest
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}