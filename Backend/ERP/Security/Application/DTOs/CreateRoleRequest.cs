namespace Security.Application.DTOs;

public class CreateRoleRequest
{
    //public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    //public bool IsSystemRole { get; set; } = false;
}