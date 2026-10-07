using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace Domain.Features.MasterData.Company;

public interface ICompanyService
{
    Task<List<CompanyEntity>> GetCompaniesAsync(Guid tenantId);

    Task<CompanyEntity?> GetCompanyAsync(
        Guid tenantId,
        Guid companyId);
}