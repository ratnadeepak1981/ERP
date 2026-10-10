namespace Domain.Features.MasterData.Branch;

public interface IBranchRepository
{
    Task<List<Branch>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId);

    Task<Branch?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId);
}