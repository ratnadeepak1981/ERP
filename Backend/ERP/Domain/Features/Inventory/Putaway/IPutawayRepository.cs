using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Inventory.Putaway;

public interface IPutawayRepository
{
    Task<Putaway?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid putawayId);
    Task<List<Putaway>> GetPutawaysAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId = null, PutawayStatus? status = null);
    Task AddAsync(Putaway putaway);
    Task UpdateAsync(Putaway putaway);
    Task<Dictionary<Guid, decimal>> GetCompletedPutawayQuantitiesByReceiptLineAsync(Guid tenantId, Guid sourceDocId);
    Task<decimal> GetCompletedPutawayQuantityBySourceLineAsync(Guid tenantId, Guid sourceDocId, Guid sourceDocLineId);
    Task<decimal> GetCompletedPutawayQuantityByProductAndReceiptAsync(Guid tenantId, Guid sourceDocId, Guid productId);
    Task<string> GenerateNextPutawayNumberAsync(Guid tenantId, int year);
}
