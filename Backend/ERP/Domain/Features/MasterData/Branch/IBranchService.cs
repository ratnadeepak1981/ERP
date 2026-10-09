namespace Domain.Features.MasterData.Branch;

public interface IBranchService
{
    Task<List<Branch>> GetBranchesAsync(
        Guid tenantId,
        Guid companyId);

    Task<Branch?> GetBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId);
}