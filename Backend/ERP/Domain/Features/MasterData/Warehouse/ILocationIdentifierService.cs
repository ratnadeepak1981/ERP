using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Warehouse;

public interface ILocationIdentifierService
{
    Task<List<LocationIdentifierDto>> GetIdentifiersAsync(Guid tenantId, Guid warehouseId, Guid locationId, bool includeInactive = false);
    Task<LocationIdentifierDto?> GetIdentifierByIdAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId);
    Task<LocationIdentifierDto> AddIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, CreateLocationIdentifierRequest request);
    Task<LocationIdentifierDto> GenerateInternalIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, GenerateInternalLocationIdentifierRequest request);
    Task<LocationIdentifierDto> SetPrimaryAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId);
    Task DeactivateIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId);
    Task<LocationIdentifierDto> ReactivateIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId, bool isPrimary = false);
}
