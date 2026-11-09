using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Warehouse;

public interface ILocationIdentifierRepository
{
    Task<WarehouseLocation?> GetLocationScopeAsync(Guid tenantId, Guid locationId);
    Task<List<LocationIdentifier>> GetIdentifiersAsync(Guid tenantId, Guid locationId, bool includeInactive = false);
    Task<LocationIdentifier?> GetIdentifierByIdAsync(Guid tenantId, Guid locationId, Guid identifierId);
    Task<bool> ExistsActiveValueAsync(Guid tenantId, string normalizedValue);
    Task DemoteExistingPrimariesAsync(Guid tenantId, Guid locationId);
    Task AddIdentifierAsync(LocationIdentifier identifier);
    Task SaveChangesAsync();
    void Detach(LocationIdentifier identifier);
}
