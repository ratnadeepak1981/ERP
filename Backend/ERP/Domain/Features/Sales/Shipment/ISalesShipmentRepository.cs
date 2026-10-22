using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.Shipment;

public interface ISalesShipmentRepository
{
    Task<List<SalesShipment>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesShipmentStatus? status = null);

    Task<List<SalesShipment>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        SalesShipmentStatus? status = null);

    Task<SalesShipment?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid shipmentId);

    Task<SalesShipment?> GetByIdAsync(
        Guid tenantId,
        Guid shipmentId);

    Task<List<SalesShipment>> GetBySalesOrderIdAsync(
        Guid tenantId,
        Guid salesOrderId);

    Task<bool> ExistsShipmentNumberAsync(
        Guid tenantId,
        Guid companyId,
        string shipmentNumber);

    Task AddAsync(SalesShipment shipment);

    Task AddItemAsync(SalesShipmentItem item);

    Task SaveChangesAsync();
}
