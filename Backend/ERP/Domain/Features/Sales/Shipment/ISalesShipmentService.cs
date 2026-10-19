using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.Shipment;

public interface ISalesShipmentService
{
    Task<List<SalesShipmentDto>> GetShipmentsAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesShipmentStatus? status = null);

    Task<SalesShipmentDto?> GetShipmentByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId);

    Task<SalesShipmentDto> CreateFromOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        CreateShipmentFromOrderRequest request);

    Task<SalesShipmentDto> CreateManualDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesShipmentRequest request);

    Task<SalesShipmentDto> UpdateDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        UpdateSalesShipmentRequest request);

    Task<SalesShipmentDto> DispatchShipmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId);

    Task<SalesShipmentDto> DeliverShipmentAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId,
        DeliverShipmentRequest? request = null);

    Task<SalesShipmentDto> CancelDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId);
}
