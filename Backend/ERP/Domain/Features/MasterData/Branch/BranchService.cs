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

    public async Task<Branch> CreateBranchAsync(
        Guid tenantId,
        Guid companyId,
        string name,
        string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Branch name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Branch code is required.", nameof(code));

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            IsActive = true
        };

        await _repository.AddAsync(branch);
        await _repository.SaveChangesAsync();

        return branch;
    }
}