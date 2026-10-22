using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.Invoice;

public interface ISalesInvoiceRepository
{
    Task<List<SalesInvoice>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesInvoiceStatus? status = null);

    Task<List<SalesInvoice>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        SalesInvoiceStatus? status = null);

    Task<SalesInvoice?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid invoiceId);

    Task<SalesInvoice?> GetByIdAsync(
        Guid tenantId,
        Guid invoiceId);

    Task<SalesInvoice?> GetByShipmentIdAsync(
        Guid tenantId,
        Guid shipmentId);

    Task<bool> ExistsInvoiceNumberAsync(
        Guid tenantId,
        Guid companyId,
        string invoiceNumber);

    Task AddAsync(SalesInvoice invoice);

    Task SaveChangesAsync();
}
