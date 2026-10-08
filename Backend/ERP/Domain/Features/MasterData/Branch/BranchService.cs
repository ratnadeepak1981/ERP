namespace Domain.Features.MasterData.Branch;

public class BranchService : IBranchService
{
    private readonly IBranchRepository _repository;

    public BranchService(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Branch>> GetBranchesAsync(
        Guid tenantId,
        Guid companyId)
    {
        return await _repository.GetByCompanyAsync(
            tenantId,
            companyId);
    }

    public async Task<Branch?> GetBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        return await _repository.GetByIdAsync(
            tenantId,
            companyId,
            branchId);
    }
}