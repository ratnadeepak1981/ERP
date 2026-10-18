namespace Security.Application.DTOs;

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Phone { get; set; }

    public string? AccountStatus { get; set; }

    public Guid? RoleId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? BranchId { get; set; }

    public bool? IsAllBranches { get; set; }
}