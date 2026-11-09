using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnitOfMeasureEntity = Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure;

namespace Domain.Features.MasterData.Product;

public interface IProductIdentifierRepository
{
    Task<Product?> GetProductAsync(Guid tenantId, Guid productId);
    Task<List<ProductIdentifier>> GetIdentifiersAsync(Guid tenantId, Guid productId, bool includeInactive = false);
    Task<ProductIdentifier?> GetIdentifierByIdAsync(Guid tenantId, Guid productId, Guid identifierId);
    Task<Dictionary<Guid, string>> GetUomCodesAsync(Guid tenantId, IEnumerable<Guid> uomIds);
    Task<string?> GetUomCodeAsync(Guid tenantId, Guid uomId);
    Task<UnitOfMeasureEntity?> GetUomByIdAsync(Guid tenantId, Guid uomId);
    Task<bool> ExistsActiveValueAsync(Guid tenantId, string normalizedValue);
    Task DemoteExistingPrimariesAsync(Guid tenantId, Guid productId, Guid? unitOfMeasureId);
    Task AddIdentifierAsync(ProductIdentifier identifier);
    Task SaveChangesAsync();
    void Detach(ProductIdentifier identifier);
}
