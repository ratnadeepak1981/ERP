using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Features.Ecommerce.Cart;

public interface IEcommerceCartService
{
    Task<CartDto> GetCartAsync(
        Guid tenantId,
        Guid companyId,
        Guid? customerId,
        string? sessionId,
        CancellationToken ct = default);

    Task<CartDto> AddToCartAsync(
        Guid tenantId,
        Guid companyId,
        AddToCartRequest request,
        Guid? customerId,
        Guid? customerContactId = null,
        CancellationToken ct = default);

    Task<CartDto> UpdateLineQuantityAsync(
        Guid tenantId,
        Guid companyId,
        Guid lineId,
        UpdateCartLineRequest request,
        Guid? customerId,
        CancellationToken ct = default);

    Task<CartDto> RemoveLineAsync(
        Guid tenantId,
        Guid companyId,
        Guid lineId,
        string? sessionId,
        Guid? customerId,
        CancellationToken ct = default);

    Task<CartDto> ClearCartAsync(
        Guid tenantId,
        Guid companyId,
        string? sessionId,
        Guid? customerId,
        CancellationToken ct = default);

    Task<CheckoutResultDto> CheckoutAsync(
        Guid tenantId,
        Guid companyId,
        CheckoutRequest request,
        Guid? customerId,
        Guid? customerContactId = null,
        Guid? actorUserId = null,
        string? actorUserName = null,
        CancellationToken ct = default);
}
