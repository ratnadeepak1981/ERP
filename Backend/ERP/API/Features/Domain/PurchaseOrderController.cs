using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseOrder;
using ERP.Application.Common.Email;
using ERP.Application.Features.Procurement.PurchaseOrderFeature;
using ERP.Application.Features.Procurement.PurchaseOrderFeature.Templates;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/companies/{companyId:guid}/branches/{branchId:guid}/purchase-orders")]
[RequirePermission("PURCHASE_ORDER.VIEW")]
public class PurchaseOrderController : BaseApiController
{
    private readonly IPurchaseOrderService _orderService;
    private readonly IPricingResolutionService _pricingResolutionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly IBranchService _branchService;
    private readonly IPurchaseOrderPdfService _pdfService;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateEngine _templateEngine;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICompanyService _companyService;

    private static readonly SortMapping<PurchaseOrder> _sortMapping = new SortMapping<PurchaseOrder>()
        .Add("orderNumber", o => o.OrderNumber, isDefault: true)
        .Add("orderDate", o => o.OrderDate)
        .Add("totalAmount", o => o.TotalAmount)
        .Add("status", o => o.Status)
        .SetSecondary(o => o.Id);

    public PurchaseOrderController(
        IPurchaseOrderService orderService,
        IPricingResolutionService pricingResolutionService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        IBranchService branchService,
        IPurchaseOrderPdfService pdfService,
        IEmailService emailService,
        IEmailTemplateEngine templateEngine,
        ISupplierRepository supplierRepository,
        ICompanyService companyService)
    {
        _orderService = orderService;
        _pricingResolutionService = pricingResolutionService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _branchService = branchService;
        _pdfService = pdfService;
        _emailService = emailService;
        _templateEngine = templateEngine;
        _supplierRepository = supplierRepository;
        _companyService = companyService;
    }

    [HttpGet("~/api/companies/{companyId:guid}/purchase-orders")]
    public async Task<IActionResult> GetCompanyOrders(
        Guid companyId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        List<PurchaseOrder> orders;
        var isTenantAdmin = User.IsInRole("Tenant Admin");
        if (isTenantAdmin)
        {
            orders = await _orderService.GetOrdersByCompanyAsync(tenantId.Value, companyId);
        }
        else
        {
            var branches = await _branchService.GetBranchesAsync(tenantId.Value, companyId);
            var accessibleBranchIds = new List<Guid>();
            foreach (var b in branches)
            {
                if (await _userAccessService.CanAccessBranchAsync(_currentUserContext.UserId, tenantId.Value, companyId, b.Id))
                {
                    accessibleBranchIds.Add(b.Id);
                }
            }
            orders = await _orderService.GetOrdersByCompanyAsync(tenantId.Value, companyId, accessibleBranchIds);
        }

        return PaginateAndSort(orders, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("~/api/companies/{companyId:guid}/purchase-orders/{id:guid}")]
    public async Task<IActionResult> GetCompanyOrder(
        Guid companyId,
        Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return Forbid();
        }

        var order = await _orderService.GetOrderByIdAsync(
            tenantId.Value,
            companyId,
            id);

        if (order == null)
        {
            return NotFoundResponse("Purchase order not found in the authorized scope.");
        }

        var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId,
            order.BranchId);

        if (!canAccessBranch)
        {
            return Forbid();
        }

        return SuccessResponse(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        Guid companyId,
        Guid branchId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var orders = await _orderService.GetOrdersAsync(
            authResult.TenantId,
            companyId,
            branchId);

        return PaginateAndSort(orders, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(
        Guid companyId,
        Guid branchId,
        Guid id)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        var order = await _orderService.GetOrderAsync(
            authResult.TenantId,
            companyId,
            branchId,
            id);

        if (order == null)
        {
            return NotFoundResponse("Purchase order not found in the authorized scope.");
        }

        return SuccessResponse(order);
    }

    [HttpPost]
    [RequirePermission("PURCHASE_ORDER.CREATE")]
    public async Task<IActionResult> CreateOrder(
        Guid companyId,
        Guid branchId,
        [FromBody] CreatePurchaseOrderRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.CreateOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                request,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return CreatedResponse(
                nameof(GetOrder),
                new { companyId, branchId, id = order.Id },
                order);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/items")]
    [RequirePermission("PURCHASE_ORDER.EDIT")]
    public async Task<IActionResult> AddItem(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] CreatePurchaseOrderItemRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var item = await _orderService.AddItemAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                request);

            return SuccessResponse(item, "Item added to purchase order successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/refresh-exchange-rate")]
    [RequirePermission("PURCHASE_ORDER.EDIT")]
    public async Task<IActionResult> RefreshExchangeRate(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] RefreshPurchaseOrderCurrencyRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.RefreshCurrencyAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                request);
            return SuccessResponse(order, "Purchase order exchange-rate snapshot refreshed.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/submit")]
    [RequirePermission("PURCHASE_ORDER.SUBMIT")]
    public async Task<IActionResult> SubmitOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.SubmitOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString());

            return SuccessResponse(order, "Purchase order submitted successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/approve")]
    [RequirePermission("PURCHASE_ORDER.APPROVE")]
    public async Task<IActionResult> ApproveOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.ApproveOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(order, "Purchase order approved successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/reject")]
    [RequirePermission("PURCHASE_ORDER.APPROVE")]
    public async Task<IActionResult> RejectOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] ApprovalDecisionRequest request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.RejectOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks ?? string.Empty);

            return SuccessResponse(order, "Purchase order rejected successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/cancel")]
    [RequirePermission("PURCHASE_ORDER.CANCEL")]
    public async Task<IActionResult> CancelOrder(
        Guid companyId,
        Guid branchId,
        Guid orderId,
        [FromBody] ApprovalDecisionRequest? request)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var order = await _orderService.CancelOrderAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId,
                _currentUserContext.UserId,
                User.Identity?.Name ?? _currentUserContext.UserId.ToString(),
                request?.Remarks);

            return SuccessResponse(order, "Purchase order cancelled successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{orderId:guid}/approval-history")]
    [RequirePermission("PURCHASE_ORDER.VIEW")]
    public async Task<IActionResult> GetApprovalHistory(
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var authResult = await ValidateScopeAccess(companyId, branchId);
        if (authResult.Action != null) return authResult.Action;

        try
        {
            var history = await _orderService.GetApprovalHistoryAsync(
                authResult.TenantId,
                companyId,
                branchId,
                orderId);

            return SuccessResponse(history);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

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

    [HttpPost("resolve-price")]
    [HttpPost("~/api/companies/{companyId:guid}/purchase-orders/resolve-price")]
    public async Task<IActionResult> ResolvePrice(
        Guid companyId,
        [FromRoute] Guid? branchId,
        [FromBody] ResolvePurchasePriceRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);
        if (!canAccessCompany) return Forbid();

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId,
                branchId.Value);
            if (!canAccessBranch) return Forbid();
        }

        if (request == null || request.ProductId == Guid.Empty)
            return BadRequestResponse("Valid request with ProductId is required.");

        var result = await _pricingResolutionService.ResolvePurchasePriceAsync(
            tenantId.Value,
            request.SupplierId,
            request.ProductId,
            request.UnitOfMeasureId,
            request.TransactionDate,
            request.PriceCardId,
            request.Currency);

        return SuccessResponse(result);
    }

    [HttpGet("{orderId:guid}/pdf")]
    [HttpGet("~/api/companies/{companyId:guid}/purchase-orders/{orderId:guid}/pdf")]
    [RequirePermission("PURCHASE_ORDER.VIEW")]
    public async Task<IActionResult> GetOrderPdf(
        Guid companyId,
        [FromRoute] Guid? branchId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var authResult = await ValidateScopeAccessOrCompany(companyId, branchId, orderId);
        if (authResult.Action != null) return authResult.Action;

        var order = await _orderService.GetOrderByIdAsync(authResult.TenantId, companyId, orderId);
        if (order == null)
            return NotFoundResponse("Purchase order not found in the authorized scope.");

        try
        {
            var pdfBytes = await _pdfService.GeneratePdfForOrderAsync(
                authResult.TenantId,
                companyId,
                orderId,
                cancellationToken);

            var fileName = order.OrderNumber.StartsWith("PO", StringComparison.OrdinalIgnoreCase)
                ? $"{order.OrderNumber}.pdf"
                : $"PO_{order.OrderNumber}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{orderId:guid}/send-email")]
    [HttpPost("~/api/companies/{companyId:guid}/purchase-orders/{orderId:guid}/send-email")]
    [RequirePermission("PURCHASE_ORDER.VIEW")]
    public async Task<IActionResult> SendOrderEmailToSupplier(
        Guid companyId,
        [FromRoute] Guid? branchId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var authResult = await ValidateScopeAccessOrCompany(companyId, branchId, orderId);
        if (authResult.Action != null) return authResult.Action;

        var order = await _orderService.GetOrderByIdAsync(authResult.TenantId, companyId, orderId);
        if (order == null)
            return NotFoundResponse("Purchase order not found in the authorized scope.");

        // Rule: Only Approved purchase orders can be emailed
        if (order.Status != PurchaseOrderStatus.Approved)
        {
            return BadRequestResponse($"Only approved purchase orders can be sent to suppliers. Current order status: '{order.Status}'.");
        }

        // Rule: Supplier must be associated
        if (!order.SupplierId.HasValue)
        {
            return BadRequestResponse("Cannot send email: No supplier is associated with this purchase order.");
        }

        var supplier = await _supplierRepository.GetSupplierByIdAsync(authResult.TenantId, order.SupplierId.Value);
        if (supplier == null)
        {
            return BadRequestResponse("Associated supplier was not found.");
        }

        // Rule: Supplier email must be configured and valid
        if (string.IsNullOrWhiteSpace(supplier.Email))
        {
            return BadRequestResponse($"Cannot send email: Supplier '{supplier.SupplierName}' does not have an email address configured in Supplier Master.");
        }

        string supplierEmail = supplier.Email.Trim();
        try
        {
            _ = new System.Net.Mail.MailAddress(supplierEmail);
        }
        catch
        {
            return BadRequestResponse($"Cannot send email: Supplier '{supplier.SupplierName}' has an invalid email address '{supplierEmail}'.");
        }

        try
        {
            // 1. Generate the PDF bytes
            var pdfBytes = await _pdfService.GeneratePdfForOrderAsync(
                authResult.TenantId,
                companyId,
                orderId,
                cancellationToken);

            // 2. Load Company details for branding
            var company = await _companyService.GetCompanyAsync(authResult.TenantId, companyId);
            string companyName = company?.Name ?? "Apex Manufacturing Ltd";

            // 3. Render HTML body via template engine
            var emailModel = new PurchaseOrderEmailModel
            {
                CompanyName = companyName,
                SupplierName = supplier.SupplierName,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                ExpectedDeliveryDate = order.ExpectedDeliveryDate?.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "TBD",
                Currency = string.IsNullOrWhiteSpace(order.Currency) ? "LKR" : order.Currency,
                TotalAmount = order.TotalAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                Remarks = "Please find the official purchase order attached. Kindly acknowledge receipt and advise your estimated delivery schedule.",
                ContactPerson = "Procurement Department",
                CompanyEmail = "procurement@apexerp.local",
                CompanyPhone = "+94 11 234 5678",
                CurrentYear = DateTime.UtcNow.Year.ToString()
            };

            string htmlBody = await _templateEngine.RenderAsync("PurchaseOrder/PurchaseOrderEmail", emailModel, cancellationToken);

            // 4. Construct EmailMessage with exact generated PDF attachment
            var emailMessage = new EmailMessage
            {
                Subject = $"Purchase Order #{order.OrderNumber} - {companyName}",
                HtmlBody = htmlBody,
                PlainTextBody = $"Dear {supplier.SupplierName},\n\nPlease find attached official Purchase Order #{order.OrderNumber} issued by {companyName}.\nTotal Amount: {emailModel.Currency} {emailModel.TotalAmount}.\n\nSincerely,\nProcurement Department\n{companyName}"
            };

            var pdfFileName = order.OrderNumber.StartsWith("PO", StringComparison.OrdinalIgnoreCase)
                ? $"{order.OrderNumber}.pdf"
                : $"PO_{order.OrderNumber}.pdf";

            emailMessage.To.Add(new EmailRecipient(supplierEmail, supplier.SupplierName));
            emailMessage.Attachments.Add(new EmailAttachment(pdfFileName, "application/pdf", pdfBytes));

            // 5. Send via centralised IEmailService
            var sendResult = await _emailService.SendEmailAsync(emailMessage, cancellationToken);

            if (!sendResult.IsSuccess)
            {
                return BadRequestResponse($"Failed to dispatch email: {sendResult.ErrorMessage}");
            }

            return SuccessResponse(new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                recipientEmail = supplierEmail,
                supplierName = supplier.SupplierName,
                attachmentFileName = pdfFileName,
                sentAtUtc = sendResult.TimestampUtc,
                message = $"Purchase Order #{order.OrderNumber} successfully sent to {supplier.SupplierName} ({supplierEmail})."
            }, "Purchase order emailed to supplier successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    private async Task<(IActionResult? Action, Guid TenantId)> ValidateScopeAccessOrCompany(
        Guid companyId,
        Guid? branchId,
        Guid orderId)
    {
        if (_currentUserContext.IsPlatformUser) return (Forbid(), Guid.Empty);
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return (UnauthorizedResponse("Tenant identity is missing or invalid."), Guid.Empty);

        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);
        if (!canAccessCompany) return (Forbid(), Guid.Empty);

        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId,
                branchId.Value);
            if (!canAccessBranch) return (Forbid(), Guid.Empty);
        }
        else
        {
            var order = await _orderService.GetOrderByIdAsync(tenantId.Value, companyId, orderId);
            if (order != null)
            {
                var canAccessBranch = await _userAccessService.CanAccessBranchAsync(
                    _currentUserContext.UserId,
                    tenantId.Value,
                    companyId,
                    order.BranchId);
                if (!canAccessBranch) return (Forbid(), Guid.Empty);
            }
        }

        return (null, tenantId.Value);
    }
}
