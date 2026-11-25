using System;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Common.TenantResolution;
using API.Features.Portal.Context;
using ERP.Application.Features.Ecommerce.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Ecommerce;

/// <summary>
/// E-commerce Shopping Cart & Free Checkout API.
/// Supports both anonymous guests (via session token) and authenticated B2B portal customers.
/// Provides real-time pricing and tax resolution, and handles free checkout generating Draft Sales Orders.
/// </summary>
[ApiController]
[Route("api/ecommerce/cart")]
[AllowAnonymous]
public class EcommerceCartController : BaseApiController
{
    public const string SessionHeaderKey = "X-Cart-Session-Id";

    private readonly IEcommerceCartService _cartService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentCustomerContext _currentCustomerContext;
    private readonly ICurrentUserContext _currentUserContext;

    public EcommerceCartController(
        IEcommerceCartService cartService,
        ITenantContext tenantContext,
        ICurrentCustomerContext currentCustomerContext,
        ICurrentUserContext currentUserContext)
    {
        _cartService = cartService ?? throw new ArgumentNullException(nameof(cartService));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _currentCustomerContext = currentCustomerContext ?? throw new ArgumentNullException(nameof(currentCustomerContext));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpGet]
    public async Task<IActionResult> GetCart(
        [FromQuery] Guid companyId,
        [FromQuery] string? sessionId = null,
        CancellationToken ct = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
            return BadRequestResponse("Tenant context could not be resolved for this request.");

        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        var (customerId, effectiveSessionId) = ResolveCustomerAndSession(sessionId);
        EnsureSessionHeader(effectiveSessionId);

        try
        {
            var cart = await _cartService.GetCartAsync(
                tenantId.Value,
                companyId,
                customerId,
                effectiveSessionId,
                ct);

            return SuccessResponse(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddToCart(
        [FromQuery] Guid companyId,
        [FromBody] AddToCartRequest request,
        CancellationToken ct = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
            return BadRequestResponse("Tenant context could not be resolved for this request.");

        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        if (request == null)
            return BadRequestResponse("Request body is required.");

        var (customerId, effectiveSessionId) = ResolveCustomerAndSession(request.SessionId);
        request.SessionId = effectiveSessionId;
        EnsureSessionHeader(effectiveSessionId);

        Guid? contactId = _currentCustomerContext.IsCustomer && _currentCustomerContext.ContactId != Guid.Empty
            ? _currentCustomerContext.ContactId
            : null;

        try
        {
            var cart = await _cartService.AddToCartAsync(
                tenantId.Value,
                companyId,
                request,
                customerId,
                contactId,
                ct);

            return SuccessResponse(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("items/{lineId:guid}")]
    public async Task<IActionResult> UpdateItemQuantity(
        [FromRoute] Guid lineId,
        [FromQuery] Guid companyId,
        [FromBody] UpdateCartLineRequest request,
        CancellationToken ct = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
            return BadRequestResponse("Tenant context could not be resolved for this request.");

        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        if (lineId == Guid.Empty)
            return BadRequestResponse("Valid LineId is required.");

        if (request == null)
            return BadRequestResponse("Request body is required.");

        var (customerId, effectiveSessionId) = ResolveCustomerAndSession(request.SessionId);
        request.SessionId = effectiveSessionId;
        EnsureSessionHeader(effectiveSessionId);

        try
        {
            var cart = await _cartService.UpdateLineQuantityAsync(
                tenantId.Value,
                companyId,
                lineId,
                request,
                customerId,
                ct);

            return SuccessResponse(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("items/{lineId:guid}")]
    public async Task<IActionResult> RemoveItem(
        [FromRoute] Guid lineId,
        [FromQuery] Guid companyId,
        [FromQuery] string? sessionId = null,
        CancellationToken ct = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
            return BadRequestResponse("Tenant context could not be resolved for this request.");

        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        if (lineId == Guid.Empty)
            return BadRequestResponse("Valid LineId is required.");

        var (customerId, effectiveSessionId) = ResolveCustomerAndSession(sessionId);
        EnsureSessionHeader(effectiveSessionId);

        try
        {
            var cart = await _cartService.RemoveLineAsync(
                tenantId.Value,
                companyId,
                lineId,
                effectiveSessionId,
                customerId,
                ct);

            return SuccessResponse(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart(
        [FromQuery] Guid companyId,
        [FromQuery] string? sessionId = null,
        CancellationToken ct = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
            return BadRequestResponse("Tenant context could not be resolved for this request.");

        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        var (customerId, effectiveSessionId) = ResolveCustomerAndSession(sessionId);
        EnsureSessionHeader(effectiveSessionId);

        try
        {
            var cart = await _cartService.ClearCartAsync(
                tenantId.Value,
                companyId,
                effectiveSessionId,
                customerId,
                ct);

            return SuccessResponse(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromQuery] Guid companyId,
        [FromBody] CheckoutRequest request,
        CancellationToken ct = default)
    {
        var tenantId = ResolveEffectiveTenantId();
        if (!tenantId.HasValue)
            return BadRequestResponse("Tenant context could not be resolved for this request.");

        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        if (request == null)
            return BadRequestResponse("Request body is required.");

        var (customerId, effectiveSessionId) = ResolveCustomerAndSession(request.SessionId);
        if (!customerId.HasValue || customerId.Value == Guid.Empty)
        {
            return UnauthorizedResponse("Authentication is required to proceed to checkout. Please log in to your account.");
        }

        request.SessionId = effectiveSessionId;
        EnsureSessionHeader(effectiveSessionId);

        Guid? contactId = _currentCustomerContext.IsCustomer && _currentCustomerContext.ContactId != Guid.Empty
            ? _currentCustomerContext.ContactId
            : null;

        Guid? actorUserId = _currentCustomerContext.IsCustomer && _currentCustomerContext.UserId != Guid.Empty
            ? _currentCustomerContext.UserId
            : (_currentUserContext.UserId != Guid.Empty ? _currentUserContext.UserId : null);

        string actorUserName = User.Identity?.Name ?? (_currentCustomerContext.IsCustomer ? "CustomerPortalUser" : "OnlineCustomer");

        try
        {
            var result = await _cartService.CheckoutAsync(
                tenantId.Value,
                companyId,
                request,
                customerId,
                contactId,
                actorUserId,
                actorUserName,
                ct);

            return SuccessResponse(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private Guid? ResolveEffectiveTenantId()
    {
        if (_tenantContext.TenantId.HasValue && _tenantContext.TenantId.Value != Guid.Empty)
            return _tenantContext.TenantId.Value;

        if (_currentCustomerContext.IsCustomer && _currentCustomerContext.TenantId != Guid.Empty)
            return _currentCustomerContext.TenantId;

        if (_currentUserContext.TenantId != Guid.Empty)
            return _currentUserContext.TenantId;

        return null;
    }

    private (Guid? CustomerId, string SessionId) ResolveCustomerAndSession(string? explicitSessionId)
    {
        Guid? customerId = null;
        if (_currentCustomerContext.IsCustomer && _currentCustomerContext.CustomerId != Guid.Empty)
        {
            customerId = _currentCustomerContext.CustomerId;
        }

        string? headerSession = Request.Headers.TryGetValue(SessionHeaderKey, out var headerVal)
            ? headerVal.ToString()
            : null;

        string sessionId = !string.IsNullOrWhiteSpace(explicitSessionId)
            ? explicitSessionId.Trim()
            : (!string.IsNullOrWhiteSpace(headerSession) ? headerSession.Trim() : Guid.NewGuid().ToString("N"));

        return (customerId, sessionId);
    }

    private void EnsureSessionHeader(string sessionId)
    {
        if (!Response.Headers.ContainsKey(SessionHeaderKey))
        {
            Response.Headers[SessionHeaderKey] = sessionId;
        }
    }
}
