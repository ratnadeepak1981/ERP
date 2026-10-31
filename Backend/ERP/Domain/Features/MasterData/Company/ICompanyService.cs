using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace Domain.Features.MasterData.Company;

public interface ICompanyService
{
    Task<List<CompanyEntity>> GetCompaniesAsync(Guid tenantId);

    Task<CompanyEntity?> GetCompanyAsync(
        Guid tenantId,
        Guid companyId);

    Task<CompanyEntity> CreateCompanyAsync(
        Guid tenantId,
        string name,
        string code,
        Guid? baseCurrencyId = null);

    Task<CompanyEntity> UpdateCompanyAsync(
        Guid tenantId,
        Guid companyId,
        string name,
        string code,
        bool isActive,
        Guid? baseCurrencyId = null,
        bool? specialUomConversionEnabled = null);
}
