using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace Domain.Features.MasterData.Company;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;

    public CompanyService(ICompanyRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CompanyEntity>> GetCompaniesAsync(
        Guid tenantId)
    {
        return await _repository.GetByTenantAsync(tenantId);
    }

    public async Task<CompanyEntity?> GetCompanyAsync(
        Guid tenantId,
        Guid companyId)
    {
        return await _repository.GetByIdAsync(
            tenantId,
            companyId);
    }
}