using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Features.Ecommerce;

public interface IEcommerceProductService
{
    Task<IReadOnlyList<EcommerceProductPublicationDto>> GetCompanyPublicationsAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default);

    Task<EcommerceProductPublicationDto> GetPublicationByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default);

    Task<EcommerceProductPublicationDto> CreatePublicationAsync(
        Guid tenantId,
        Guid companyId,
        CreateEcommerceProductRequest request,
        CancellationToken ct = default);

    Task<EcommerceProductPublicationDto> UpdatePublicationAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        UpdateEcommerceProductRequest request,
        CancellationToken ct = default);

    Task<EcommerceProductPublicationDto> PublishAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default);

    Task<EcommerceProductPublicationDto> UnpublishAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default);

    Task<bool> DeletePublicationAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PublicEcommerceProductDto>> GetPublicCatalogueAsync(
        Guid companyId,
        PublicCatalogueFilter? filter = null,
        Guid? tenantId = null,
        CancellationToken ct = default);

    Task<PublicEcommerceProductDto> GetPublicProductDetailAsync(
        Guid companyId,
        Guid productId,
        Guid? tenantId = null,
        CancellationToken ct = default);
}
