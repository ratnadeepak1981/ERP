using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.SalesQuotation;

public interface ISalesQuotationRepository
{
    Task<List<SalesQuotation>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesQuotationStatus? status = null);

    Task<SalesQuotation?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId);

    Task<SalesQuotation?> GetByIdUnscopedAsync(
        Guid tenantId,
        Guid quotationId);

    Task<List<SalesQuotation>> GetByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        SalesQuotationStatus? status = null);

    Task<bool> ExistsQuotationNumberAsync(
        Guid tenantId,
        Guid companyId,
        string quotationNumber);

    Task<int> GetCountByCompanyAsync(
        Guid tenantId,
        Guid companyId);

    Task AddAsync(SalesQuotation quotation);

    Task AddItemAsync(SalesQuotationItem item);

    Task SaveChangesAsync();
}
