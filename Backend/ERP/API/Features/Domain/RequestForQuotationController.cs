using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Domain.Features.Procurement.RequestForQuotation;
using Domain.Features.Procurement.Route;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SaaS.Application.Interfaces;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/rfqs")]
[RequirePermission("RFQ.VIEW")]
public class RequestForQuotationController : BaseApiController
{
    private readonly IRFQService _rfqService;
    private readonly ISupplierQuotationService _quotationService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly IProcurementRouteService? _routeService;

    public RequestForQuotationController(
        IRFQService rfqService,
        ISupplierQuotationService quotationService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        IProcurementRouteService? routeService = null)
    {
        _rfqService = rfqService;
        _quotationService = quotationService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _routeService = routeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRfqs(
        Guid companyId,
        Guid branchId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        RfqFilterRequest? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestForQuotationStatus>(status, true, out var parsedStatus))
        {
            filter = new RfqFilterRequest { Status = parsedStatus };
        }

        var list = await _rfqService.GetRfqsAsync(authResult.TenantId, companyId, branchId, filter, ct);
        return SuccessResponse(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRfqById(Guid companyId, Guid branchId, Guid id, CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var rfq = await _rfqService.GetRfqByIdAsync(id, authResult.TenantId, ct);
        if (rfq == null || rfq.CompanyId != companyId || rfq.BranchId != branchId)
            return NotFoundResponse($"RFQ with ID {id} not found in the authorized scope.");

        // Quotation commercial data is permission-controlled separately from the RFQ header.
        if (!CanViewQuotations())
        {
            // If the RFQ is already Awarded, retain the approved winning quotation so downstream PO generation can proceed.
            rfq.Quotations = rfq.Quotations
                .Where(q => q.Status == SupplierQuotationStatus.Approved)
                .ToList();
        }

        return SuccessResponse(rfq);
    }

    [HttpPost]
    [RequirePermission("RFQ.CREATE")]
    public async Task<IActionResult> CreateRfq(
        Guid companyId,
        Guid branchId,
        [FromBody] CreateRfqRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var created = await _rfqService.CreateRfqAsync(request, userId, authResult.TenantId, companyId, branchId, ct);
        return CreatedResponse(created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("RFQ.EDIT")]
    public async Task<IActionResult> UpdateRfq(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] UpdateRfqRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.UpdateRfqAsync(id, request, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    [HttpPost("{id:guid}/lines")]
    [RequirePermission("RFQ.EDIT")]
    public async Task<IActionResult> AddLine(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] CreateRfqLineRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.AddLineAsync(id, request, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [RequirePermission("RFQ.EDIT")]
    public async Task<IActionResult> RemoveLine(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid lineId,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.RemoveLineAsync(id, lineId, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    [HttpPost("{id:guid}/suppliers")]
    [RequirePermission("RFQ.SUPPLIER.MANAGE")]
    public async Task<IActionResult> AddSupplier(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] AddRfqSupplierRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.AddSupplierAsync(id, request, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    [HttpDelete("{id:guid}/suppliers/{supplierId:guid}")]
    [RequirePermission("RFQ.SUPPLIER.MANAGE")]
    public async Task<IActionResult> RemoveSupplier(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid supplierId,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.RemoveSupplierAsync(id, supplierId, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    [HttpPost("{id:guid}/send-invitations")]
    [RequirePermission("RFQ.SEND")]
    public async Task<IActionResult> SendInvitations(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] SendRfqInvitationsRequest request,
        [FromServices] ITenantDomainRepository tenantDomainRepository,
        [FromServices] IConfiguration configuration,
        [FromServices] global::Domain.Common.ApplicationUrl.IApplicationUrlService urlService,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        // Auto-resolve browser-accessible portal base URL via ApplicationUrlService if not supplied
        if (string.IsNullOrWhiteSpace(request.PortalBaseUrl))
        {
            request.PortalBaseUrl = urlService.ResolveBaseUrl();
        }

        // Auto-resolve tenant subdomain from repository if not explicitly passed
        if (string.IsNullOrWhiteSpace(request.TenantSubdomain))
        {
            var tenantDomains = await tenantDomainRepository.GetByTenantIdAsync(authResult.TenantId);
            var primaryDomain = tenantDomains.FirstOrDefault(d => d.IsPrimary && d.IsActive) ?? tenantDomains.FirstOrDefault(d => d.IsActive);
            if (primaryDomain != null && !string.IsNullOrWhiteSpace(primaryDomain.Subdomain))
            {
                request.TenantSubdomain = primaryDomain.Subdomain;
            }
        }

        // Auto-resolve external base domain from configuration if not explicitly passed
        if (string.IsNullOrWhiteSpace(request.ExternalBaseUrl))
        {
            var configuredExternal = configuration["Hosting:ExternalBaseDomain"] ?? configuration["Hosting:TenantBaseDomain"];
            if (!string.IsNullOrWhiteSpace(configuredExternal) && configuredExternal != "localhost")
            {
                request.ExternalBaseUrl = configuredExternal;
            }
        }

        var userId = _currentUserContext.UserId;
        var result = await _rfqService.SendInvitationsAsync(id, request, userId, authResult.TenantId, ct);
        return SuccessResponse(result);
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission("RFQ.EDIT")]
    public async Task<IActionResult> CloseRfq(Guid companyId, Guid branchId, Guid id, CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.CloseRfqAsync(id, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("RFQ.EDIT")]
    public async Task<IActionResult> CancelRfq(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] CancelRfqRequest? request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var updated = await _rfqService.CancelRfqAsync(id, request?.Reason, userId, authResult.TenantId, ct);
        return SuccessResponse(updated);
    }

    // Quotation Endpoints
    [HttpGet("{id:guid}/quotations")]
    [RequirePermission("RFQ.QUOTATION.VIEW")]
    public async Task<IActionResult> GetQuotations(Guid companyId, Guid branchId, Guid id, CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var quotes = await _quotationService.GetQuotationsForRfqAsync(id, authResult.TenantId, companyId, branchId, ct);
        return SuccessResponse(quotes);
    }

    [HttpGet("{id:guid}/quotations/{quotationId:guid}")]
    [RequirePermission("RFQ.QUOTATION.VIEW")]
    public async Task<IActionResult> GetQuotationById(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid quotationId,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var quote = await _quotationService.GetQuotationByIdAsync(id, quotationId, authResult.TenantId, companyId, branchId, ct);
        if (quote == null)
        {
            return NotFoundResponse("Quotation not found.");
        }

        return SuccessResponse(quote);
    }

    [HttpPost("{id:guid}/manual-quotations")]
    [RequirePermission("RFQ.QUOTATION.RECORD")]
    public async Task<IActionResult> RecordManualQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        [FromBody] RecordManualQuotationRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var created = await _quotationService.RecordManualQuotationAsync(id, request, userId, authResult.TenantId, companyId, branchId, ct);
        return CreatedResponse(created);
    }

    [HttpGet("{id:guid}/comparison-matrix")]
    [RequirePermission("RFQ.COMPARE")]
    public async Task<IActionResult> GetComparisonMatrix(Guid companyId, Guid branchId, Guid id, CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var matrix = await _quotationService.GetComparisonMatrixAsync(id, authResult.TenantId, companyId, branchId, ct);
        return SuccessResponse(matrix);
    }

    [HttpPost("{id:guid}/quotations/{quotationId:guid}/approve")]
    [RequirePermission("RFQ.APPROVE")]
    public async Task<IActionResult> ApproveQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid quotationId,
        [FromBody] ApproveQuotationRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var result = await _quotationService.ApproveQuotationAsync(id, quotationId, request, userId, authResult.TenantId, companyId, branchId, ct);
        return SuccessResponse(result);
    }

    [HttpPost("{id:guid}/quotations/{quotationId:guid}/reject")]
    [RequirePermission("RFQ.REJECT")]
    public async Task<IActionResult> RejectQuotation(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid quotationId,
        [FromBody] RejectQuotationRequest request,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var result = await _quotationService.RejectQuotationAsync(id, quotationId, request, userId, authResult.TenantId, companyId, branchId, ct);
        return SuccessResponse(result);
    }

    [HttpPost("{id:guid}/quotations/{quotationId:guid}/retry-award-notification")]
    [RequirePermission("RFQ.SEND")]
    public async Task<IActionResult> RetryAwardNotification(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid quotationId,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var userId = _currentUserContext.UserId;
        var result = await _quotationService.RetryAwardNotificationAsync(id, quotationId, userId, authResult.TenantId, companyId, branchId, ct);
        return SuccessResponse(result);
    }

    [HttpGet("{id:guid}/quotations/{quotationId:guid}/validate-po")]
    [RequirePermission("PURCHASE_ORDER.CREATE")]
    public async Task<IActionResult> ValidateAwardPo(
        Guid companyId,
        Guid branchId,
        Guid id,
        Guid quotationId,
        CancellationToken ct = default)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        if (_routeService == null)
            return StatusCode(500, "Procurement route service is not configured.");

        try
        {
            await _routeService.ValidateRfqAwardPoRouteAsync(
                authResult.TenantId,
                companyId,
                branchId,
                id,
                quotationId,
                ct);

            return SuccessResponse(new { eligible = true });
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private bool CanViewQuotations() =>
        User.HasClaim("permission", "RFQ.QUOTATION.VIEW") ||
        User.HasClaim("permission", "RFQ.VIEW") ||
        User.HasClaim("permission", "RFQ.AWARD") ||
        User.HasClaim("permission", "PURCHASE_ORDER.CREATE") ||
        User.IsInRole("TenantAdmin") ||
        User.IsInRole("PlatformAdmin") ||
        _currentUserContext.IsPlatformUser;

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccess(
        Guid companyId,
        Guid branchId)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return (Forbid(), Guid.Empty);
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return (UnauthorizedResponse("Tenant identity is missing or invalid."), Guid.Empty);
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return (Forbid(), Guid.Empty);
        }

        var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId,
            branchId);

        if (!canAccessBranch)
        {
            return (Forbid(), Guid.Empty);
        }

        return (null, tenantId.Value);
    }
}
