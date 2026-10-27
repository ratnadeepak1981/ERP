using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory.Planning;

public interface IInventoryPlanningService
{
    Task<List<ReplenishmentSuggestionDto>> GetReplenishmentSuggestionsAsync(
        Guid tenantId,
        Guid? warehouseId = null,
        Guid? categoryId = null,
        DateTime? asOfDate = null);
}
