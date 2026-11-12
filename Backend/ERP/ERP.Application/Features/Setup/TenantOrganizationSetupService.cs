using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Setup;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AddressEntity = Domain.Features.MasterData.Address.Address;
using BranchEntity = Domain.Features.MasterData.Branch.Branch;
using CompanyEntity = Domain.Features.MasterData.Company.Company;
using CurrencyEntity = Domain.Features.MasterData.Currency.Currency;

namespace ERP.Application.Features.Setup;

public class TenantOrganizationSetupService : ITenantOrganizationSetupService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ICompanyService _companyService;
    private readonly IBranchRepository _branchRepository;
    private readonly IBranchService _branchService;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly DomainDbContext _domainDbContext;

    // Standard static AddressType IDs from Global Reference Data
    private static readonly Guid RegOfficeAddressTypeId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid BranchAddressTypeId = Guid.Parse("11111111-0000-0000-0000-000000000007");

    public TenantOrganizationSetupService(
        ICompanyRepository companyRepository,
        ICompanyService companyService,
        IBranchRepository branchRepository,
        IBranchService branchService,
        ICurrencyRepository currencyRepository,
        IAddressRepository addressRepository,
        DomainDbContext domainDbContext)
    {
        _companyRepository = companyRepository ?? throw new ArgumentNullException(nameof(companyRepository));
        _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _branchService = branchService ?? throw new ArgumentNullException(nameof(branchService));
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _addressRepository = addressRepository ?? throw new ArgumentNullException(nameof(addressRepository));
        _domainDbContext = domainDbContext ?? throw new ArgumentNullException(nameof(domainDbContext));
    }

    public async Task<TenantOrganizationSetupResult> SetupOrganizationAsync(
        Guid tenantId,
        TenantOrganizationSetupRequest? request = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Valid Tenant ID is required.", nameof(tenantId));
        }

        var result = new TenantOrganizationSetupResult
        {
            TenantId = tenantId
        };

        // 1. Resolve or Ensure Base Currency
        string currencyCode = !string.IsNullOrWhiteSpace(request?.BaseCurrencyCode)
            ? request.BaseCurrencyCode.Trim().ToUpperInvariant()
            : "LKR";

        var baseCurrency = await _currencyRepository.GetCurrencyByCodeAsync(tenantId, currencyCode);
        if (baseCurrency == null)
        {
            // Seed base currency if not yet present in DomainDB for this tenant
            baseCurrency = currencyCode switch
            {
                "USD" => CurrencyEntity.Create(tenantId, "USD", "US Dollar", "$", 2),
                "EUR" => CurrencyEntity.Create(tenantId, "EUR", "Euro", "€", 2),
                "GBP" => CurrencyEntity.Create(tenantId, "GBP", "British Pound", "£", 2),
                "INR" => CurrencyEntity.Create(tenantId, "INR", "Indian Rupee", "₹", 2),
                _ => CurrencyEntity.Create(tenantId, "LKR", "Sri Lankan Rupee", "Rs", 2)
            };

            await _currencyRepository.AddCurrencyAsync(baseCurrency);
            await _currencyRepository.SaveChangesAsync();
            result.Messages.Add($"Created tenant base currency '{currencyCode}' ({baseCurrency.CurrencyId}).");
        }

        // 2. Company Resolution or Creation
        string companyCode = !string.IsNullOrWhiteSpace(request?.CompanyCode)
            ? request.CompanyCode.Trim().ToUpperInvariant()
            : "MAIN";

        string companyName = !string.IsNullOrWhiteSpace(request?.CompanyName)
            ? request.CompanyName.Trim()
            : $"Company {companyCode}";

        var existingCompanies = await _companyRepository.GetByTenantAsync(tenantId);
        var company = existingCompanies.FirstOrDefault(c =>
            string.Equals(c.Code, companyCode, StringComparison.OrdinalIgnoreCase))
            ?? existingCompanies.FirstOrDefault();

        if (company == null)
        {
            company = await _companyService.CreateCompanyAsync(
                tenantId,
                companyName,
                companyCode,
                baseCurrency.CurrencyId);

            result.CompanyCreated = true;
            result.Messages.Add($"Created company '{company.Name}' with code '{company.Code}'.");
        }
        else
        {
            result.CompanyCreated = false;
            result.Messages.Add($"Reused existing company '{company.Name}' ({company.Code}).");
        }

        result.CompanyId = company.Id;
        result.CompanyCode = company.Code;
        result.CompanyName = company.Name;

        // 3. Branch Resolution or Creation
        string branchCode = !string.IsNullOrWhiteSpace(request?.BranchCode)
            ? request.BranchCode.Trim().ToUpperInvariant()
            : "MAIN";

        string branchName = !string.IsNullOrWhiteSpace(request?.BranchName)
            ? request.BranchName.Trim()
            : $"{company.Name} Main Branch";

        var existingBranches = await _branchRepository.GetByCompanyAsync(tenantId, company.Id);
        var branch = existingBranches.FirstOrDefault(b =>
            string.Equals(b.Code, branchCode, StringComparison.OrdinalIgnoreCase))
            ?? existingBranches.FirstOrDefault();

        if (branch == null)
        {
            branch = await _branchService.CreateBranchAsync(
                tenantId,
                company.Id,
                branchName,
                branchCode);

            result.BranchCreated = true;
            result.Messages.Add($"Created branch '{branch.Name}' with code '{branch.Code}'.");
        }
        else
        {
            result.BranchCreated = false;
            result.Messages.Add($"Reused existing branch '{branch.Name}' ({branch.Code}).");
        }

        result.BranchId = branch.Id;
        result.BranchCode = branch.Code;
        result.BranchName = branch.Name;

        // 4. Resolve Country
        var countries = await _addressRepository.GetCountriesAsync();
        var country = !string.IsNullOrWhiteSpace(request?.CountryName)
            ? countries.FirstOrDefault(c => string.Equals(c.CountryName, request.CountryName, StringComparison.OrdinalIgnoreCase))
            : countries.FirstOrDefault(c => c.CountryName == "Sri Lanka") ?? countries.FirstOrDefault();

        // 5. Company Address Association
        var existingCompanyAddress = await _domainDbContext.CompanyAddresses
            .FirstOrDefaultAsync(ca =>
                ca.TenantId == tenantId &&
                ca.CompanyId == company.Id &&
                ca.AddressTypeId == RegOfficeAddressTypeId &&
                ca.IsActive);

        if (existingCompanyAddress == null)
        {
            string line1 = request?.AddressLine1 ?? "No. 123, Galle Road";
            string line2 = request?.AddressLine2 ?? "Kollupitiya";
            string city = request?.City ?? "Colombo 03";
            string state = request?.StateProvince ?? "Western Province";
            string postal = request?.PostalCode ?? "00300";

            var compAddr = AddressEntity.Create(
                tenantId,
                $"{company.Name} Registered Office",
                line1,
                city,
                addressLine2: line2,
                stateProvince: state,
                postalCode: postal,
                countryId: country?.CountryId);

            await _addressRepository.AddAddressAsync(compAddr);
            await _addressRepository.SaveChangesAsync();

            var compLink = CompanyAddress.Create(
                tenantId,
                company.Id,
                compAddr.AddressId,
                RegOfficeAddressTypeId,
                isDefault: true);

            await _domainDbContext.CompanyAddresses.AddAsync(compLink);
            await _domainDbContext.SaveChangesAsync();

            result.CompanyAddressId = compAddr.AddressId;
            result.CompanyAddressCreated = true;
            result.Messages.Add($"Created registered office address for company '{company.Name}'.");
        }
        else
        {
            result.CompanyAddressId = existingCompanyAddress.AddressId;
            result.CompanyAddressCreated = false;
            result.Messages.Add($"Reused existing registered office address for company '{company.Name}'.");
        }

        // 6. Branch Address Association
        var existingBranchAddress = await _domainDbContext.BranchAddresses
            .FirstOrDefaultAsync(ba =>
                ba.TenantId == tenantId &&
                ba.BranchId == branch.Id &&
                ba.AddressTypeId == BranchAddressTypeId &&
                ba.IsActive);

        if (existingBranchAddress == null)
        {
            string bLine1 = request?.AddressLine1 ?? "No. 123, Galle Road";
            string bLine2 = request?.AddressLine2 ?? "Kollupitiya";
            string bCity = request?.City ?? "Colombo 03";
            string bState = request?.StateProvince ?? "Western Province";
            string bPostal = request?.PostalCode ?? "00300";

            var branchAddr = AddressEntity.Create(
                tenantId,
                $"{branch.Name} Operating Location",
                bLine1,
                bCity,
                addressLine2: bLine2,
                stateProvince: bState,
                postalCode: bPostal,
                countryId: country?.CountryId);

            await _addressRepository.AddAddressAsync(branchAddr);
            await _addressRepository.SaveChangesAsync();

            var branchLink = BranchAddress.Create(
                tenantId,
                branch.Id,
                branchAddr.AddressId,
                BranchAddressTypeId,
                isDefault: true);

            await _domainDbContext.BranchAddresses.AddAsync(branchLink);
            await _domainDbContext.SaveChangesAsync();

            result.BranchAddressId = branchAddr.AddressId;
            result.BranchAddressCreated = true;
            result.Messages.Add($"Created operating location address for branch '{branch.Name}'.");
        }
        else
        {
            result.BranchAddressId = existingBranchAddress.AddressId;
            result.BranchAddressCreated = false;
            result.Messages.Add($"Reused existing operating location address for branch '{branch.Name}'.");
        }

        return result;
    }
}
