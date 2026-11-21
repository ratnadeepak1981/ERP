using System;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.Context;
using Domain.Features.Sales.SalesQuotation;
using ERP.Application.Features.Sales.SalesQuotation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Portal;

/// <summary>
/// Authenticated Customer Portal controller for Sales Quotations.
/// Manages customer quotation requests, proposals review, decision responses (Accept/Decline),
/// request cloning from historical transactions, and formal PDF document downloads.
/// Follows Sales-Quotation-Workflow.md Channel C and Customer-Workspace-and-Registration-Workflow.md.
/// </summary>
[ApiController]
[Route("api/portal/customer/quotations")]
[Authorize]
public class CustomerQuotationController : BaseApiController
{
    private readonly ISalesQuotationService _quotationService;
    private readonly ICurrentCustomerContext _customerContext;

    public CustomerQuotationController(
        ISalesQuotationService quotationService,
        ICurrentCustomerContext customerContext)
    {
        _quotationService = quotationService ?? throw new ArgumentNullException(nameof(quotationService));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
    }

    [HttpGet]
    public async Task<IActionResult> GetQuotations(
        [FromQuery] SalesQuotationStatus? status = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        var quotes = await _quotationService.GetCustomerQuotationsAsync(
            _customerContext.TenantId,
            _customerContext.CustomerId,
            status);

        return SuccessResponse(quotes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetQuotationDetails(
        Guid id,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        var quote = await _quotationService.GetCustomerQuotationDetailsAsync(
            _customerContext.TenantId,
            _customerContext.CustomerId,
            id);

        if (quote == null)
            return NotFoundResponse("Sales quotation not found in authorized customer account.");

        return SuccessResponse(quote);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadQuotationPdf(
        Guid id,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        try
        {
            var pdfBytes = await _quotationService.GenerateQuotationPdfAsync(
                _customerContext.TenantId,
                id);

            return File(pdfBytes, "application/pdf", $"SalesQuotation_{id:N}.pdf");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("request")]
    public async Task<IActionResult> SubmitQuotationRequest(
        [FromQuery] Guid companyId,
        [FromQuery] Guid branchId,
        [FromBody] CreateCustomerQuotationRequest request,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (companyId == Guid.Empty || branchId == Guid.Empty)
            return BadRequestResponse("Selling company ID and branch ID are required.");

        if (request == null)
            return BadRequestResponse("Quotation request payload is required.");

        request.CustomerId = _customerContext.CustomerId;
        request.CustomerContactId = _customerContext.ContactId;

        try
        {
            var quote = await _quotationService.CreateCustomerQuotationRequestAsync(
                _customerContext.TenantId,
                companyId,
                branchId,
                request,
                _customerContext.ContactId,
                "Customer Portal User");

            return CreatedResponse(quote, "Sales quotation request submitted successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
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

    [HttpPost("request/clone-order/{orderId:guid}")]
    public async Task<IActionResult> CloneFromOrder(
        Guid orderId,
        [FromQuery] Guid companyId,
        [FromQuery] Guid branchId,
        [FromBody] CloneQuotationRequest? request = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (companyId == Guid.Empty || branchId == Guid.Empty)
            return BadRequestResponse("Selling company ID and branch ID are required.");

        try
        {
            var quote = await _quotationService.CloneCustomerQuotationRequestFromOrderAsync(
                _customerContext.TenantId,
                companyId,
                branchId,
                _customerContext.CustomerId,
                _customerContext.ContactId,
                orderId,
                request?.CustomerRemarks);

            return CreatedResponse(quote, "Quotation request successfully cloned from historical Sales Order.");
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
            return ConflictResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("request/clone-quotation/{quotationId:guid}")]
    public async Task<IActionResult> CloneFromQuotation(
        Guid quotationId,
        [FromQuery] Guid companyId,
        [FromQuery] Guid branchId,
        [FromBody] CloneQuotationRequest? request = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (companyId == Guid.Empty || branchId == Guid.Empty)
            return BadRequestResponse("Selling company ID and branch ID are required.");

        try
        {
            var quote = await _quotationService.CloneCustomerQuotationRequestFromQuotationAsync(
                _customerContext.TenantId,
                companyId,
                branchId,
                _customerContext.CustomerId,
                _customerContext.ContactId,
                quotationId,
                request?.CustomerRemarks);

            return CreatedResponse(quote, "Quotation request successfully cloned from prior Sales Quotation.");
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
            return ConflictResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> AcceptQuotation(
        Guid id,
        [FromBody] AcceptQuotationRequest? request = null,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (!_customerContext.Permissions.Contains("PORTAL_QUOTATION.ACCEPT"))
            return Forbid("Purchasing authority required to accept formal commercial proposals.");

        try
        {
            var quote = await _quotationService.AcceptQuotationAndConvertAtomicAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                _customerContext.ContactId,
                id,
                request);

            return SuccessResponse(quote, "Quotation accepted and converted into a Sales Order draft successfully.");
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

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> DeclineQuotation(
        Guid id,
        [FromBody] DeclineQuotationRequest request,
        CancellationToken ct = default)
    {
        if (!_customerContext.IsCustomer)
            return Forbid();

        if (request == null || string.IsNullOrWhiteSpace(request.Remarks))
            return BadRequestResponse("Remarks are required when declining a quotation.");

        try
        {
            var quote = await _quotationService.DeclineQuotationAsync(
                _customerContext.TenantId,
                _customerContext.CustomerId,
                _customerContext.ContactId,
                id,
                request);

            return SuccessResponse(quote, "Quotation declined successfully.");
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
