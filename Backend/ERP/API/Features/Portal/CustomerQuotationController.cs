using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Features.Portal.Context;
using Domain.Features.MasterData.Address;
using Domain.Features.Sales.SalesQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Sales.SalesQuotation;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly DomainDbContext _domainContext;

    public CustomerQuotationController(
        ISalesQuotationService quotationService,
        ICurrentCustomerContext customerContext,
        DomainDbContext domainContext)
    {
        _quotationService = quotationService ?? throw new ArgumentNullException(nameof(quotationService));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
        _domainContext = domainContext ?? throw new ArgumentNullException(nameof(domainContext));
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

        // Corporate/Company customer authorization check: Individual/Retail customers cannot submit Company Quotation Requests
        bool isIndividualRetail = _customerContext.Roles.Any(r =>
            string.Equals(r, "CUSTOMER_INDIVIDUAL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Customer Individual", StringComparison.OrdinalIgnoreCase));

        bool isCompanyCustomer = _customerContext.Roles.Any(r =>
            r.Contains("CUSTOMER_COMPANY", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Customer Company", StringComparison.OrdinalIgnoreCase));

        if (isIndividualRetail || !isCompanyCustomer)
            return ForbiddenResponse("Individual/Retail customers cannot submit Company Sales Quotation requests.");

        // Purchasing permission check
        bool hasPurchasingAuthority = _customerContext.Permissions.Contains("PORTAL_QUOTATION.REQUEST")
            || _customerContext.Permissions.Contains("PORTAL.QUOTATION.REQUEST");

        if (!hasPurchasingAuthority)
            return ForbiddenResponse("Purchasing authority required to submit Sales Quotation requests.");

        if (request == null)
            return BadRequestResponse("Quotation request payload is required.");

        if (request.Items == null || request.Items.Count == 0)
            return BadRequestResponse("At least one product line item is required.");

        // 1. Enforce authenticated Customer Master identity (never trust frontend-supplied IDs)
        request.CustomerId = _customerContext.CustomerId;
        request.CustomerContactId = _customerContext.ContactId;

        // 2. Validate Customer Master is active and approved in current tenant
        var customer = await _domainContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == _customerContext.TenantId && c.CustomerId == _customerContext.CustomerId && c.IsActive, ct);

        if (customer == null)
            return UnauthorizedResponse("Customer company is inactive or not found.");

        // 3. Resolve Selling Company if not provided
        if (companyId == Guid.Empty)
        {
            var defaultCompany = await _domainContext.Companies
                .AsNoTracking()
                .Where(c => c.TenantId == _customerContext.TenantId && c.IsActive)
                .OrderBy(c => c.Name)
                .FirstOrDefaultAsync(ct);

            if (defaultCompany == null)
                return BadRequestResponse("No active selling company found for this tenant.");

            companyId = defaultCompany.Id;
        }

        // 4. Resolve Selling Branch if not provided
        if (branchId == Guid.Empty)
        {
            var defaultBranch = await _domainContext.Branches
                .AsNoTracking()
                .Where(b => b.CompanyId == companyId && b.IsActive)
                .OrderBy(b => b.Name)
                .FirstOrDefaultAsync(ct);

            if (defaultBranch == null)
                return BadRequestResponse("Selling company has no active branches available to receive quotation requests.");

            branchId = defaultBranch.Id;
        }

        // 5. Validate Delivery Address Ownership if provided
        if (request.DeliveryAddressId.HasValue && request.DeliveryAddressId.Value != Guid.Empty)
        {
            var addressQuery = from ca in _domainContext.CustomerAddresses
                               join a in _domainContext.Addresses on new { ca.TenantId, ca.AddressId } equals new { a.TenantId, a.AddressId }
                               where ca.TenantId == _customerContext.TenantId
                                  && ca.CustomerId == _customerContext.CustomerId
                                  && (ca.AddressId == request.DeliveryAddressId.Value || ca.CustomerAddressId == request.DeliveryAddressId.Value)
                                  && ca.IsActive && a.IsActive
                               select a;

            var matchedAddress = await addressQuery.AsNoTracking().FirstOrDefaultAsync(ct);

            if (matchedAddress == null)
                return BadRequestResponse("Selected delivery address does not belong to your company account.");

            if (string.IsNullOrWhiteSpace(request.DeliveryAddressSnapshot))
            {
                request.DeliveryAddressSnapshot = $"{matchedAddress.AddressLine1}, {matchedAddress.City}, {matchedAddress.PostalCode}".Trim().TrimEnd(',');
            }
        }

        // 6. Duplicate Submission Protection (via Idempotency Key)
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var keyTag = $"[IdempotencyKey:{request.IdempotencyKey.Trim()}]";
            var existing = await _domainContext.SalesQuotations
                .AsNoTracking()
                .FirstOrDefaultAsync(sq => sq.TenantId == _customerContext.TenantId
                    && sq.CustomerId == _customerContext.CustomerId
                    && ((sq.InternalNotes != null && sq.InternalNotes.Contains(keyTag))
                        || (sq.CustomerRemarks != null && sq.CustomerRemarks.Contains(keyTag))), ct);

            if (existing != null)
            {
                var existingDto = await _quotationService.GetCustomerQuotationDetailsAsync(_customerContext.TenantId, _customerContext.CustomerId, existing.Id);
                return SuccessResponse(existingDto, "Quotation request was already processed.");
            }
        }

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

        bool hasAcceptAuthority = _customerContext.Permissions.Contains("PORTAL_QUOTATION.ACCEPT")
            || _customerContext.Permissions.Contains("PORTAL.QUOTATION.ACCEPT")
            || _customerContext.Roles.Any(r => r.Contains("CUSTOMER_COMPANY_ADMIN", StringComparison.OrdinalIgnoreCase)
                                            || r.Contains("Company Administrator", StringComparison.OrdinalIgnoreCase));

        if (!hasAcceptAuthority)
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
