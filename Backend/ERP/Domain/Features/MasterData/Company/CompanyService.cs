using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace Domain.Features.MasterData.Company;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;
    private readonly ICurrencyRepository _currencyRepository;

    public CompanyService(
        ICompanyRepository repository,
        ICurrencyRepository currencyRepository)
    {
        _repository = repository;
        _currencyRepository = currencyRepository;
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

    public async Task<CompanyEntity> CreateCompanyAsync(
        Guid tenantId,
        string name,
        string code,
        Guid? baseCurrencyId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Company code is required.", nameof(code));

        var baseCurrency = await GetActiveBaseCurrencyAsync(tenantId, baseCurrencyId);

        var company = new CompanyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            IsActive = true,
            SpecialUomConversionEnabled = true,
            BaseCurrencyId = baseCurrency.CurrencyId
        };

        await _repository.AddAsync(company);
        await _repository.SaveChangesAsync();

        return await _repository.GetByIdAsync(tenantId, company.Id) ?? company;
    }

    public async Task<CompanyEntity> UpdateCompanyAsync(
        Guid tenantId,
        Guid companyId,
        string name,
        string code,
        bool isActive,
        Guid? baseCurrencyId = null,
        bool? specialUomConversionEnabled = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Company code is required.", nameof(code));

        var company = await _repository.GetByIdAsync(tenantId, companyId);
        if (company == null)
            throw new InvalidOperationException("Company not found.");

        var baseCurrency = await GetActiveBaseCurrencyAsync(tenantId, baseCurrencyId);

        company.Name = name.Trim();
        company.Code = code.Trim().ToUpperInvariant();
        company.IsActive = isActive;
        company.BaseCurrencyId = baseCurrency.CurrencyId;
        if (specialUomConversionEnabled.HasValue)
        {
            company.SpecialUomConversionEnabled = specialUomConversionEnabled.Value;
        }

        await _repository.UpdateAsync(company);
        await _repository.SaveChangesAsync();

        return await _repository.GetByIdAsync(tenantId, companyId) ?? company;
    }

    private async Task<Domain.Features.MasterData.Currency.Currency> GetActiveBaseCurrencyAsync(Guid tenantId, Guid? currencyId)
    {
        if (!currencyId.HasValue || currencyId.Value == Guid.Empty)
            throw new InvalidOperationException("An active company base currency is required.");

        var currency = await _currencyRepository.GetCurrencyByIdAsync(tenantId, currencyId.Value);
        if (currency == null || currency.TenantId != tenantId)
            throw new InvalidOperationException("Base currency does not exist or does not belong to this tenant.");
        if (!currency.IsActive)
            throw new InvalidOperationException("The selected base currency is inactive.");

        return currency;
    }
}
