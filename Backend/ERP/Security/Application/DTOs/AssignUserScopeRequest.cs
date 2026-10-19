namespace Security.Application.DTOs;

public class AssignUserScopeRequest
{
    public Guid UserId { get; set; }

    public Guid? RoleId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? BranchId { get; set; }

    public bool IsAllBranches { get; set; }

    public string? Notes { get; set; }
}
