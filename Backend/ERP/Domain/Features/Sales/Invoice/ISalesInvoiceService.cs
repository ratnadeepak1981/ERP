using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.Invoice;

public interface ISalesInvoiceService
{
    Task<List<SalesInvoiceDto>> GetInvoicesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        SalesInvoiceStatus? status = null);

    Task<SalesInvoiceDto?> GetInvoiceByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        Guid invoiceId);

    Task<SalesInvoiceDto?> GetInvoiceByShipmentIdAsync(
        Guid tenantId,
        Guid shipmentId);

    Task<SalesInvoiceDto> CreateDraftFromShipmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        string? invoiceNumber = null);

    Task<SalesInvoiceDto> UpdateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid id,
        UpdateSalesInvoiceRequest request);

    Task<SalesInvoiceDto> ConfirmInvoiceAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid confirmedBy,
        string? notes = null);

    Task<SalesInvoiceDto> CancelInvoiceAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid id);
}
