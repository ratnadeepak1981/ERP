using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.Context;
using Domain.Features.Sales.SalesOrder;
using ERP.Application.Features.Sales.CustomerPortal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Portal;

/// <summary>
/// Authenticated Customer Portal controller managing customer order visibility,
/// 8-stage fulfillment tracking, customer invoice summaries, and dashboard KPIs.
/// Strictly scrubs all internal employee notes, reviewer identities, and cost margins.
/// </summary>
[ApiController]
[Route("api/portal/customer")]
[Authorize]
public class CustomerOrderController : BaseApiController
{
    private readonly ICustomerOrderPortalService _portalService;
    private readonly ICurrentCustomerContext _customerContext;

    public CustomerOrderController(
        ICustomerOrderPortalService portalService,
        ICurrentCustomerContext customerContext)
    {
        _portalService = portalService ?? throw new ArgumentNullException(nameof(portalService));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] Guid? sellingCompanyId = null,
        [FromQuery] SalesOrderStatus? status = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var orders = await _portalService.GetOrdersAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                sellingCompanyId,
                status,
                ct);

            return SuccessResponse(orders);
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

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var order = await _portalService.GetOrderByIdAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                id,
                ct);

            return SuccessResponse(order);
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

    [HttpGet("orders/{id:guid}/tracking")]
    public async Task<IActionResult> GetOrderTracking(Guid id, CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var tracking = await _portalService.GetOrderTrackingAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                id,
                ct);

            return SuccessResponse(tracking);
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

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? sellingCompanyId = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var invoices = await _portalService.GetInvoicesAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                sellingCompanyId,
                ct);

            return SuccessResponse(invoices);
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

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? sellingCompanyId = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        try
        {
            var dashboard = await _portalService.GetDashboardAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                sellingCompanyId,
                ct);

            return SuccessResponse(dashboard);
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
}
