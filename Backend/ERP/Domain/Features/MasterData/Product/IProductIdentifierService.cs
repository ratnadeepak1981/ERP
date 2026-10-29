using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Product;

public interface IProductIdentifierService
{
    Task<List<ProductIdentifierDto>> GetIdentifiersAsync(Guid tenantId, Guid productId, bool includeInactive = false);
    Task<ProductIdentifierDto?> GetIdentifierByIdAsync(Guid tenantId, Guid productId, Guid identifierId);
    Task<ProductIdentifierDto> AddIdentifierAsync(Guid tenantId, Guid productId, CreateProductIdentifierRequest request);
    Task<ProductIdentifierDto> GenerateInternalIdentifierAsync(Guid tenantId, Guid productId, GenerateInternalProductIdentifierRequest request);
    Task<ProductIdentifierDto> SetPrimaryAsync(Guid tenantId, Guid productId, Guid identifierId);
    Task DeactivateIdentifierAsync(Guid tenantId, Guid productId, Guid identifierId);
    Task<ProductIdentifierDto> ReactivateIdentifierAsync(Guid tenantId, Guid productId, Guid identifierId, bool isPrimary = false);
}
