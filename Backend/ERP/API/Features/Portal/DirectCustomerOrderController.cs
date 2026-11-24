using System;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.Context;
using ERP.Application.Features.Sales.DirectOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Portal;

/// <summary>
/// Authenticated Customer Portal controller for Direct Company Sales (B2B Price Card purchasing).
/// Follows Sales-Quotation-Workflow.md Channel B and Customer-Workspace-and-Registration-Workflow.md.
/// </summary>
[ApiController]
[Route("api/portal/customer/orders")]
[Authorize]
public class DirectCustomerOrderController : BaseApiController
{
    private readonly IDirectCustomerOrderService _directOrderService;
    private readonly ICurrentCustomerContext _customerContext;

    public DirectCustomerOrderController(
        IDirectCustomerOrderService directOrderService,
        ICurrentCustomerContext customerContext)
    {
        _directOrderService = directOrderService ?? throw new ArgumentNullException(nameof(directOrderService));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
    }

    [HttpPost("direct-b2b")]
    public async Task<IActionResult> CreateDirectB2BOrder(
        [FromBody] CreateDirectB2BOrderRequest request,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
        {
            return Forbid();
        }

        bool hasOrderAuthority = _customerContext.Permissions.Contains("PORTAL_ORDER.SUBMIT")
            || _customerContext.Permissions.Contains("PORTAL.ORDER.SUBMIT")
            || _customerContext.Roles.Any(r => r.Contains("CUSTOMER_COMPANY_ADMIN", StringComparison.OrdinalIgnoreCase)
                                            || r.Contains("Company Administrator", StringComparison.OrdinalIgnoreCase));

        if (!hasOrderAuthority)
        {
            return Forbid("Purchasing authority required to place direct contract orders.");
        }

        if (request == null)
        {
            return BadRequestResponse("Order request payload is required.");
        }

        try
        {
            var order = await _directOrderService.CreateDirectB2BOrderAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                _customerContext.ContactId,
                request,
                ct);

            return CreatedResponse(order, "Direct B2B sales order created in Draft status.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return UnauthorizedResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
