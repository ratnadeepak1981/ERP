using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Setup;

public class TenantMasterDataSetupRequest
{
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string? ProfileCode { get; set; }
    public string? ProfileJson { get; set; }
}

public class TenantMasterDataSetupResult
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }
    public bool Success { get; set; }

    public int CurrenciesProcessed { get; set; }
    public int ExchangeRatesProcessed { get; set; }
    public int PaymentTermsProcessed { get; set; }
    public int TaxCodesProcessed { get; set; }
    public int CategoriesProcessed { get; set; }
    public int BrandsProcessed { get; set; }
    public int UomsProcessed { get; set; }
    public int ConversionsProcessed { get; set; }
    public int ProductsProcessed { get; set; }
    public int PackagingsProcessed { get; set; }
    public int IdentifiersProcessed { get; set; }
    public int SuppliersProcessed { get; set; }
    public int CustomersProcessed { get; set; }
    public int PriceCardsProcessed { get; set; }
    public int WarehousesProcessed { get; set; }
    public int ZonesProcessed { get; set; }
    public int LocationsProcessed { get; set; }

    public List<string> Messages { get; set; } = new();
}

public interface ITenantMasterDataSetupService
{
    Task<TenantMasterDataSetupResult> SetupMasterDataAsync(
        Guid tenantId,
        TenantMasterDataSetupRequest? request = null);
}
