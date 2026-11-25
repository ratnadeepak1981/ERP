using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.Approval;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.SalesQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.SalesQuotation;

public class SalesQuotationService : ISalesQuotationService
{
    private readonly ISalesQuotationRepository _repository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly ITaxCodeRepository? _taxCodeRepository;
    private readonly ICurrencyRepository? _currencyRepository;
    private readonly IExchangeRateService? _exchangeRateService;
    private readonly IApprovalWorkflowService _approvalService;
    private readonly ISalesPersistenceCoordinator? _persistenceCoordinator;
    private readonly ISalesOrderRepository? _salesOrderRepository;
    private readonly IPriceCardRepository? _priceCardRepository;
    private readonly ISalesQuotationPdfService? _pdfService;
    private readonly ISalesQuotationEmailService? _emailService;
    private readonly DomainDbContext? _dbContext;

    public SalesQuotationService(
        ISalesQuotationRepository repository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfMeasureRepository uomRepository,
        IApprovalWorkflowService approvalService,
        ITaxCodeRepository? taxCodeRepository = null,
        ICurrencyRepository? currencyRepository = null,
        IExchangeRateService? exchangeRateService = null,
        ISalesPersistenceCoordinator? persistenceCoordinator = null,
        ISalesOrderRepository? salesOrderRepository = null,
        IPriceCardRepository? priceCardRepository = null,
        ISalesQuotationPdfService? pdfService = null,
        ISalesQuotationEmailService? emailService = null,
        DomainDbContext? dbContext = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _uomRepository = uomRepository ?? throw new ArgumentNullException(nameof(uomRepository));
        _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
        _taxCodeRepository = taxCodeRepository;
        _currencyRepository = currencyRepository;
        _exchangeRateService = exchangeRateService;
        _persistenceCoordinator = persistenceCoordinator;
        _salesOrderRepository = salesOrderRepository;
        _priceCardRepository = priceCardRepository;
        _pdfService = pdfService;
        _emailService = emailService;
        _dbContext = dbContext;
    }

    public async Task<List<SalesQuotationDto>> GetQuotationsAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesQuotationStatus? status = null,
        SalesQuotationType? documentType = null)
    {
        var quotations = await _repository.GetByBranchAsync(tenantId, companyId, branchId, status, documentType);
        var dtos = quotations.Select(MapToDto).ToList();
        foreach (var dto in dtos)
        {
            await EnrichQuotationDtoAsync(dto, tenantId);
        }
        return dtos;
    }

    public async Task<SalesQuotationDto?> GetQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null) return null;
        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<List<SalesQuotationDto>> GetCustomerQuotationsAsync(
        Guid tenantId,
        Guid customerId,
        SalesQuotationStatus? status = null)
    {
        var quotations = await _repository.GetByCustomerAsync(tenantId, customerId, status);
        return quotations.Select(MapToDto).ToList();
    }

    public async Task<SalesQuotationDto> CreateQuotationDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesQuotationDraftRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.QuotationNumber))
        {
            int count = await _repository.GetCountByCompanyAsync(tenantId, companyId);
            request.QuotationNumber = $"SQ-{DateTime.UtcNow.Year}-{(count + 1):D4}";
        }

        string quotationNumber = request.QuotationNumber.Trim();

        if (await _repository.ExistsQuotationNumberAsync(tenantId, companyId, quotationNumber))
        {
            throw new InvalidOperationException($"Sales Quotation '{quotationNumber}' already exists for this company.");
        }

        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, request.CustomerId);
        if (customer == null)
        {
            throw new InvalidOperationException($"Customer '{request.CustomerId}' was not found or does not belong to this tenant.");
        }

        if (!customer.IsActive)
        {
            throw new InvalidOperationException($"Customer '{customer.CustomerCode}' is inactive.");
        }

        DateTime validUntil = request.ValidUntilDateUtc != default
            ? request.ValidUntilDateUtc
            : DateTime.UtcNow.AddDays(30);

        string currencyCode = !string.IsNullOrWhiteSpace(request.Currency)
            ? request.Currency.Trim()
            : (!string.IsNullOrWhiteSpace(customer.CurrencyCode) ? customer.CurrencyCode : "USD");

        var quotation = global::Domain.Features.Sales.SalesQuotation.SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customer.CustomerId,
            quotationNumber,
            validUntil,
            documentType: SalesQuotationType.Standard,
            quotationDate: request.QuotationDate ?? DateTime.UtcNow,
            deliveryDate: request.DeliveryDate,
            currency: currencyCode,
            paymentTerms: request.PaymentTerms ?? customer.PaymentTerms,
            deliveryTerms: request.DeliveryTerms,
            notes: request.Notes,
            internalNotes: request.InternalNotes,
            createdByUserId: actorUserId,
            customerContactId: request.CustomerContactId,
            deliveryAddressId: request.DeliveryAddressId,
            deliveryAddressSnapshot: request.DeliveryAddressSnapshot,
            priceCardId: request.PriceCardId ?? customer.DefaultSalesPriceCardId);

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, itemReq);
                decimal taxRate = await ResolveTaxRateAsync(tenantId, itemReq.TaxCodeId, itemReq.TaxRate, quotation.QuotationDate);

                quotation.AddItem(
                    product.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    itemReq.DiscountRate,
                    itemReq.TaxCodeId,
                    taxRate,
                    uomId,
                    itemReq.IsTaxInclusive,
                    itemReq.Notes);
            }
        }

        await ApplyCurrencySnapshotAsync(quotation, currencyCode);

        await _repository.AddAsync(quotation);
        await _repository.SaveChangesAsync();

        return MapToDto(quotation);
    }

    public async Task<SalesQuotationDto> CreateCustomerQuotationRequestAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateCustomerQuotationRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, request.CustomerId);
        if (customer == null)
        {
            throw new InvalidOperationException($"Customer '{request.CustomerId}' was not found or does not belong to this tenant.");
        }

        if (!customer.IsActive)
        {
            throw new InvalidOperationException($"Customer '{customer.CustomerCode}' is inactive.");
        }

        int count = await _repository.GetCountByCompanyAsync(tenantId, companyId);
        string quotationNumber = $"SQ-{DateTime.UtcNow.Year}-{(count + 1):D4}";

        // Default 30-day validity for requested quotations
        DateTime validUntil = DateTime.UtcNow.AddDays(30);

        string currencyCode = !string.IsNullOrWhiteSpace(request.Currency)
            ? request.Currency.Trim()
            : (!string.IsNullOrWhiteSpace(customer.CurrencyCode) ? customer.CurrencyCode : "USD");

        var quotation = global::Domain.Features.Sales.SalesQuotation.SalesQuotation.CreateCustomerRequest(
            tenantId,
            companyId,
            branchId,
            customer.CustomerId,
            request.CustomerContactId ?? Guid.Empty,
            quotationNumber,
            validUntil,
            customerRemarks: request.CustomerRemarks,
            currency: currencyCode,
            createdByUserId: actorUserId);

        quotation.DeliveryDate = request.RequiredDeliveryDate;
        quotation.PaymentTerms = customer.PaymentTerms;
        quotation.PriceCardId = customer.DefaultSalesPriceCardId;
        quotation.DeliveryAddressId = request.DeliveryAddressId;
        quotation.DeliveryAddressSnapshot = request.DeliveryAddressSnapshot;

        var remarksParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.CustomerRemarks))
            remarksParts.Add(request.CustomerRemarks.Trim());
        if (!string.IsNullOrWhiteSpace(request.CustomerReference))
            remarksParts.Add($"Customer Ref: {request.CustomerReference.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.ProjectReference))
            remarksParts.Add($"Project: {request.ProjectReference.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.DeliveryInstructions))
            remarksParts.Add($"Delivery Instructions: {request.DeliveryInstructions.Trim()}");

        if (remarksParts.Count > 0)
        {
            quotation.CustomerRemarks = string.Join(" | ", remarksParts);
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? $"[IdempotencyKey:{request.IdempotencyKey.Trim()}]"
                : $"{quotation.InternalNotes}\n[IdempotencyKey:{request.IdempotencyKey.Trim()}]";
        }

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, itemReq);
                decimal taxRate = await ResolveTaxRateAsync(tenantId, itemReq.TaxCodeId, itemReq.TaxRate, quotation.QuotationDate);

                quotation.AddItem(
                    product.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    itemReq.DiscountRate,
                    itemReq.TaxCodeId,
                    taxRate,
                    uomId,
                    itemReq.IsTaxInclusive,
                    itemReq.Notes);
            }
        }

        await ApplyCurrencySnapshotAsync(quotation, currencyCode);

        // Preserve original customer request snapshot immutably in internal notes
        var originalSnapshot = new OriginalCustomerRequestDto
        {
            CustomerRemarks = request.CustomerRemarks,
            DeliveryAddressSnapshot = request.DeliveryAddressSnapshot,
            RequiredDeliveryDate = request.RequiredDeliveryDate,
            Items = request.Items?.Select(i => new OriginalCustomerRequestItemDto
            {
                ProductId = i.ProductId,
                RequestedQuantity = i.Quantity,
                TargetUnitPrice = i.UnitPrice,
                Notes = i.Notes
            }).ToList() ?? new()
        };
        string snapshotMarker = $"[ORIGINAL_REQUEST_SNAPSHOT:{System.Text.Json.JsonSerializer.Serialize(originalSnapshot)}]";
        quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
            ? snapshotMarker
            : $"{quotation.InternalNotes}\n{snapshotMarker}";

        // If submitted by customer via portal or with customer contact, submit quotation request for internal review (Status: Submitted)
        if (!request.IsDraft && quotation.Items.Any(i => i.IsActive) &&
            (request.CustomerContactId.HasValue || (actorUserName != null && actorUserName.Contains("Portal"))))
        {
            quotation.SubmitForApproval(actorUserId ?? customer.CustomerId);
        }

        await _repository.AddAsync(quotation);
        await _repository.SaveChangesAsync();

        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> UpdateQuotationDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        UpdateSalesQuotationDraftRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        if (quotation.Status != SalesQuotationStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot update quotation in status '{quotation.Status}'. Only Draft quotations can be updated.");
        }

        if (request.QuotationDate.HasValue) quotation.QuotationDate = request.QuotationDate.Value;
        if (request.ValidUntilDateUtc.HasValue) quotation.SetValidUntilDate(request.ValidUntilDateUtc.Value);
        if (request.DeliveryDate.HasValue) quotation.DeliveryDate = request.DeliveryDate.Value;
        if (!string.IsNullOrWhiteSpace(request.PaymentTerms)) quotation.PaymentTerms = request.PaymentTerms.Trim();
        if (!string.IsNullOrWhiteSpace(request.DeliveryTerms)) quotation.DeliveryTerms = request.DeliveryTerms.Trim();
        if (request.Notes != null) quotation.Notes = request.Notes;
        if (request.InternalNotes != null)
        {
            string? existingSnapshot = null;
            if (!string.IsNullOrWhiteSpace(quotation.InternalNotes) && quotation.InternalNotes.Contains("[ORIGINAL_REQUEST_SNAPSHOT:"))
            {
                int snapIdx = quotation.InternalNotes.IndexOf("[ORIGINAL_REQUEST_SNAPSHOT:", StringComparison.Ordinal);
                int endSnap = quotation.InternalNotes.IndexOf("]", snapIdx, StringComparison.Ordinal);
                if (endSnap > snapIdx)
                {
                    existingSnapshot = quotation.InternalNotes.Substring(snapIdx, endSnap - snapIdx + 1);
                }
            }

            if (existingSnapshot != null && !request.InternalNotes.Contains("[ORIGINAL_REQUEST_SNAPSHOT:"))
            {
                quotation.InternalNotes = $"{request.InternalNotes}\n{existingSnapshot}";
            }
            else
            {
                quotation.InternalNotes = request.InternalNotes;
            }
        }
        if (request.DeliveryAddressId.HasValue) quotation.DeliveryAddressId = request.DeliveryAddressId.Value;
        if (request.DeliveryAddressSnapshot != null) quotation.DeliveryAddressSnapshot = request.DeliveryAddressSnapshot;

        if (!string.IsNullOrWhiteSpace(request.Currency) && !string.Equals(quotation.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            quotation.Currency = request.Currency.Trim().ToUpperInvariant();
            await ApplyCurrencySnapshotAsync(quotation, quotation.Currency);
        }

        if (request.Items != null)
        {
            foreach (var existing in quotation.Items.Where(i => i.IsActive).ToList())
            {
                existing.IsActive = false;
            }

            foreach (var itemReq in request.Items)
            {
                var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, itemReq);
                decimal taxRate = await ResolveTaxRateAsync(tenantId, itemReq.TaxCodeId, itemReq.TaxRate, quotation.QuotationDate);

                quotation.AddItem(
                    product.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    itemReq.DiscountRate,
                    itemReq.TaxCodeId,
                    taxRate,
                    uomId,
                    itemReq.IsTaxInclusive,
                    itemReq.Notes);
            }
        }

        quotation.RecalculateTotals();
        await _repository.SaveChangesAsync();

        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationItemDto> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        CreateSalesQuotationItemRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        var (product, uomId, unitPrice) = await ValidateAndResolveItemAsync(tenantId, request);
        decimal taxRate = await ResolveTaxRateAsync(tenantId, request.TaxCodeId, request.TaxRate, quotation.QuotationDate);

        var item = quotation.AddItem(
            product.ProductId,
            request.Quantity,
            unitPrice,
            request.DiscountRate,
            request.TaxCodeId,
            taxRate,
            uomId,
            request.IsTaxInclusive,
            request.Notes);

        await _repository.SaveChangesAsync();
        return MapItemToDto(item);
    }

    public async Task<SalesQuotationItemDto> UpdateItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid itemId,
        UpdateSalesQuotationItemRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        var item = quotation.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
        if (item == null)
        {
            throw new InvalidOperationException($"Quotation item '{itemId}' was not found.");
        }

        decimal taxRate = await ResolveTaxRateAsync(tenantId, request.TaxCodeId, request.TaxRate, quotation.QuotationDate);

        quotation.UpdateItem(
            itemId,
            request.Quantity,
            request.UnitPrice,
            request.DiscountRate,
            request.TaxCodeId,
            taxRate,
            request.UnitOfMeasureId ?? item.UnitOfMeasureId,
            request.IsTaxInclusive,
            request.Notes);

        await _repository.SaveChangesAsync();
        return MapItemToDto(item);
    }

    public async Task RemoveItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid itemId)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        quotation.RemoveItem(itemId);
        await _repository.SaveChangesAsync();
    }

    public async Task<SalesQuotationDto> SubmitQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        if (quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            throw new InvalidOperationException("Customer Requests cannot be submitted for commercial approval. Please prepare a formal Commercial Quotation first.");
        }

        // Domain validation & state transition: Draft -> Submitted
        quotation.SubmitForApproval(actorUserId);

        // Record in generic Approval Audit log
        await _approvalService.RecordSubmissionAsync(
            tenantId,
            "SalesQuotation",
            quotation.Id,
            actorUserId,
            actorUserName,
            quotation.TotalAmount);

        await _repository.SaveChangesAsync();
        var submitDto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(submitDto, tenantId);
        return submitDto;
    }

    public async Task<SalesQuotationDto> ApproveQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        if (quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            throw new InvalidOperationException("Customer Requests cannot be approved as commercial offers. Please prepare a formal Commercial Quotation first.");
        }

        // Segregation of duties: The user who submitted the transaction cannot approve it
        var submitterUserId = quotation.SubmittedByUserId ?? quotation.CreatedByUserId;
        if (approverUserId != Guid.Empty && submitterUserId.HasValue && approverUserId == submitterUserId.Value)
        {
            throw new InvalidOperationException("Users are not permitted to approve their own transactions.");
        }

        var creatorOrSubmitter = quotation.SubmittedByUserId ?? quotation.CreatedByUserId ?? Guid.Empty;

        // Validate and transition domain state FIRST: Submitted -> Confirmed
        quotation.Approve(approverUserId);

        // Enforce approval record & self-approval guard via ApprovalWorkflowService
        await _approvalService.RecordApprovalAsync(
            tenantId,
            "SalesQuotation",
            quotation.Id,
            approverUserId,
            approverUserName,
            creatorOrSubmitter,
            quotation.TotalAmount,
            remarks);

        await _repository.SaveChangesAsync();
        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> RejectQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid approverUserId,
        string approverUserName,
        string remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new ArgumentException("Remarks are mandatory when rejecting a quotation internally.", nameof(remarks));
        }

        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        if (quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            throw new InvalidOperationException("Customer Requests cannot be rejected via commercial offer approval. Please prepare a commercial quotation or close the request.");
        }

        // Validate and transition domain state FIRST: Submitted -> RejectedInternal
        quotation.Reject(approverUserId, remarks);

        // Record in approval audit log
        await _approvalService.RecordRejectionAsync(
            tenantId,
            "SalesQuotation",
            quotation.Id,
            approverUserId,
            approverUserName,
            remarks,
            quotation.TotalAmount);

        await _repository.SaveChangesAsync();
        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> CancelQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException($"Sales quotation '{quotationId}' was not found.");
        }

        if (quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            throw new InvalidOperationException("Customer Requests cannot be cancelled through commercial quotation cancellation. Please reject or close the customer request directly.");
        }

        quotation.Cancel(actorUserId);

        await _approvalService.RecordCancellationAsync(
            tenantId,
            "SalesQuotation",
            quotation.Id,
            actorUserId,
            actorUserName,
            quotation.TotalAmount,
            remarks);

        await _repository.SaveChangesAsync();
        var cancelDto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(cancelDto, tenantId);
        return cancelDto;
    }

    public async Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid quotationId)
    {
        return await _approvalService.GetApprovalHistoryAsync(tenantId, "SalesQuotation", quotationId);
    }

    public async Task<SalesQuotationDto> ReturnToDraftAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId)
            ?? throw new KeyNotFoundException($"Sales quotation '{quotationId}' was not found.");

        quotation.ReturnToDraft(actorUserId, remarks);

        await _repository.SaveChangesAsync();
        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> PrepareCommercialQuotationFromRequestAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerRequestId,
        Guid actorUserId,
        string actorUserName)
    {
        var customerRequest = await _repository.GetByIdAsync(tenantId, companyId, branchId, customerRequestId);
        if (customerRequest == null)
        {
            throw new KeyNotFoundException($"Customer request '{customerRequestId}' was not found.");
        }

        if (customerRequest.DocumentType != SalesQuotationType.CustomerRequest)
        {
            throw new InvalidOperationException($"Sales quotation '{customerRequestId}' is not a customer request.");
        }

        // If a commercial quotation has already been prepared and is active, return that one to prevent duplicate offers
        if (!string.IsNullOrWhiteSpace(customerRequest.InternalNotes) && customerRequest.InternalNotes.Contains("[LinkedCommercialQuotationId:"))
        {
            var matchCom = System.Text.RegularExpressions.Regex.Match(customerRequest.InternalNotes, @"\[LinkedCommercialQuotationId:([0-9a-fA-F-]+)");
            if (matchCom.Success && Guid.TryParse(matchCom.Groups[1].Value, out var existingQuotationId))
            {
                var existingQuotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, existingQuotationId);
                if (existingQuotation != null && existingQuotation.Status != SalesQuotationStatus.Cancelled)
                {
                    var existingDto = MapToDto(existingQuotation);
                    await EnrichQuotationDtoAsync(existingDto, tenantId);
                    return existingDto;
                }
            }
        }

        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, customerRequest.CustomerId);
        if (customer == null)
        {
            throw new InvalidOperationException($"Customer '{customerRequest.CustomerId}' was not found.");
        }

        int count = await _repository.GetCountByCompanyAsync(tenantId, companyId);
        string quotationNumber = $"SQ-{DateTime.UtcNow.Year}-{(count + 1):D4}";
        DateTime validUntil = DateTime.UtcNow.AddDays(30);

        // Create formal Commercial Quotation (DocumentType = Standard, Status = Draft)
        var commercialQuotation = global::Domain.Features.Sales.SalesQuotation.SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerRequest.CustomerId,
            quotationNumber,
            validUntil,
            documentType: SalesQuotationType.Standard,
            notes: customerRequest.Notes,
            currency: customerRequest.Currency,
            customerRemarks: customerRequest.CustomerRemarks,
            createdByUserId: actorUserId,
            customerContactId: customerRequest.CustomerContactId,
            deliveryAddressId: customerRequest.DeliveryAddressId,
            deliveryAddressSnapshot: customerRequest.DeliveryAddressSnapshot,
            priceCardId: customerRequest.PriceCardId ?? customer.DefaultSalesPriceCardId);

        commercialQuotation.DeliveryDate = customerRequest.DeliveryDate;
        commercialQuotation.PaymentTerms = customerRequest.PaymentTerms ?? customer.PaymentTerms;
        commercialQuotation.DeliveryTerms = customerRequest.DeliveryTerms;

        // Traceability linking marker on the new Commercial Quotation
        string quotationLinkMarker = $"[LinkedCustomerRequestId:{customerRequest.Id}|RequestNumber:{customerRequest.QuotationNumber}]";
        commercialQuotation.InternalNotes = string.IsNullOrWhiteSpace(commercialQuotation.InternalNotes)
            ? quotationLinkMarker
            : $"{commercialQuotation.InternalNotes}\n{quotationLinkMarker}";

        // Copy requested product lines as initial proposal
        foreach (var reqItem in customerRequest.Items.Where(i => i.IsActive))
        {
            commercialQuotation.AddItem(
                reqItem.ProductId,
                reqItem.Quantity,
                reqItem.UnitPrice,
                reqItem.DiscountRate,
                reqItem.TaxCodeId,
                reqItem.TaxRate,
                reqItem.UnitOfMeasureId,
                reqItem.IsTaxInclusive,
                reqItem.Notes);
        }

        await ApplyCurrencySnapshotAsync(commercialQuotation, customerRequest.Currency);

        // Save new Commercial Quotation
        await _repository.AddAsync(commercialQuotation);
        await _repository.SaveChangesAsync();

        // Traceability linking marker and Review Status update on the original Customer Request
        string requestLinkMarker = $"[LinkedCommercialQuotationId:{commercialQuotation.Id}|QuotationNumber:{commercialQuotation.QuotationNumber}][RequestReviewStatus:QuotationPrepared]";
        customerRequest.InternalNotes = string.IsNullOrWhiteSpace(customerRequest.InternalNotes)
            ? requestLinkMarker
            : $"{customerRequest.InternalNotes}\n{requestLinkMarker}";

        await _repository.SaveChangesAsync();

        var dto = MapToDto(commercialQuotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> SendQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName,
        bool isRetry = false)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId)
            ?? throw new KeyNotFoundException($"Sales quotation '{quotationId}' was not found in authorized scope.");

        if (quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            throw new InvalidOperationException("Customer Requests cannot be dispatched as commercial offers. Please prepare a formal Commercial Quotation first.");
        }

        if (quotation.IsExpired())
        {
            throw new InvalidOperationException($"Cannot send quotation {quotation.QuotationNumber} because it has expired.");
        }

        // Rule 1: Only approved (Confirmed) offers can be sent to customers (or Sent if retrying)
        if (quotation.Status != SalesQuotationStatus.Confirmed && !(isRetry && quotation.Status == SalesQuotationStatus.Sent))
        {
            throw new InvalidOperationException($"Cannot send quotation in status '{quotation.Status}'. Only approved (Confirmed) quotations can be sent to customers.");
        }

        // Rule 2: Prevent accidental duplicate notifications
        if (quotation.SentAtUtc.HasValue && !isRetry)
        {
            throw new InvalidOperationException($"Quotation {quotation.QuotationNumber} was already sent on {quotation.SentAtUtc.Value:yyyy-MM-dd HH:mm:ss} UTC. Enable retry if resending is required.");
        }

        // Rule 3: Dispatch email notification (failure must never reverse internal approval)
        if (_emailService != null)
        {
            SalesQuotationEmailDispatchResult dispatchResult;
            try
            {
                dispatchResult = await _emailService.SendQuotationNotificationAsync(quotation);
            }
            catch (Exception ex)
            {
                string failedMarker = $"[EmailStatus:Failed|Recipient:Unknown|Error:{ex.Message}]";
                quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                    ? failedMarker
                    : $"{quotation.InternalNotes}\n{failedMarker}";

                await _repository.SaveChangesAsync();
                throw new InvalidOperationException($"Failed to send quotation email: {ex.Message}", ex);
            }

            if (dispatchResult.IsSuccess)
            {
                quotation.MarkSent(DateTime.UtcNow);
                string sentMarker = $"[EmailStatus:Sent|Recipient:{dispatchResult.RecipientEmail}|SentAt:{DateTime.UtcNow:O}]";
                quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                    ? sentMarker
                    : $"{quotation.InternalNotes}\n{sentMarker}";
            }
            else
            {
                string failedMarker = $"[EmailStatus:Failed|Recipient:{dispatchResult.RecipientEmail ?? "Unknown"}|Error:{dispatchResult.ErrorMessage ?? "Dispatch failed"}]";
                quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                    ? failedMarker
                    : $"{quotation.InternalNotes}\n{failedMarker}";

                await _repository.SaveChangesAsync();
                throw new InvalidOperationException($"Failed to send quotation email: {dispatchResult.ErrorMessage}");
            }
        }
        else
        {
            quotation.MarkSent(DateTime.UtcNow);
        }

        await _repository.SaveChangesAsync();
        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> AcceptQuotationAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        Guid quotationId,
        AcceptQuotationRequest? request = null)
    {
        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new InvalidOperationException("Customer account is inactive or not found.");

        if (!customer.IsActive)
            throw new InvalidOperationException($"Customer '{customer.CustomerCode}' is inactive.");

        var quotation = await _repository.GetByIdUnscopedAsync(tenantId, quotationId)
            ?? throw new KeyNotFoundException("Quotation not found in authorized customer account.");

        if (quotation.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to accept quotations belonging to another customer.");

        // Idempotency: If already accepted (and not yet converted), return existing accepted quotation
        if (quotation.Status == SalesQuotationStatus.Accepted)
        {
            var acceptedDto = MapToDto(quotation);
            await EnrichQuotationDtoAsync(acceptedDto, tenantId);
            return acceptedDto;
        }

        if (quotation.ConvertedSalesOrderId.HasValue || quotation.Status == SalesQuotationStatus.Converted)
            throw new InvalidOperationException("This quotation has already been converted into a Sales Order.");

        if (quotation.Status != SalesQuotationStatus.Sent && quotation.Status != SalesQuotationStatus.Confirmed)
            throw new InvalidOperationException($"Cannot accept quotation with status '{quotation.Status}'. Only formally approved or issued quotations can be accepted.");

        if (quotation.IsExpired())
            throw new InvalidOperationException("Quotation has expired and cannot be accepted. Please request a formal quotation revision.");

        var now = DateTime.UtcNow;

        // Delivery address preference
        if (request?.DeliveryAddressId.HasValue == true && request.DeliveryAddressId.Value != Guid.Empty)
        {
            if (_dbContext != null)
            {
                var isLinked = await _dbContext.CustomerAddresses
                    .AnyAsync(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.AddressId == request.DeliveryAddressId.Value && ca.IsActive);

                if (!isLinked)
                {
                    throw new InvalidOperationException($"The specified delivery address '{request.DeliveryAddressId.Value}' does not belong to this customer account or is inactive.");
                }

                var addr = await _dbContext.Addresses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AddressId == request.DeliveryAddressId.Value && a.IsActive);

                if (addr != null)
                {
                    quotation.DeliveryAddressSnapshot = $"{addr.AddressLine1}, {addr.City}".Trim(',', ' ');
                }
            }

            quotation.DeliveryAddressId = request.DeliveryAddressId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request?.CustomerRemarks))
        {
            string cleanRemarks = CleanCustomerRemarks(request.CustomerRemarks) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(cleanRemarks))
            {
                quotation.CustomerRemarks = string.IsNullOrWhiteSpace(quotation.CustomerRemarks)
                    ? cleanRemarks
                    : $"{quotation.CustomerRemarks} | {cleanRemarks}";
            }
        }

        // Record customer acceptance only (NO Sales Order creation!)
        quotation.Accept(customerContactId, now);

        await _repository.SaveChangesAsync();
        var dto = MapToDto(quotation);
        await EnrichQuotationDtoAsync(dto, tenantId);
        return dto;
    }

    public async Task<SalesQuotationDto> AcceptQuotationAndConvertAtomicAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        Guid quotationId,
        AcceptQuotationRequest? request = null)
    {
        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, customerId)
            ?? throw new InvalidOperationException("Customer account is inactive or not found.");

        if (!customer.IsActive)
            throw new InvalidOperationException($"Customer '{customer.CustomerCode}' is inactive.");

        var quotation = await _repository.GetByIdUnscopedAsync(tenantId, quotationId)
            ?? throw new KeyNotFoundException("Quotation not found in authorized customer account.");

        if (quotation.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to accept quotations belonging to another customer.");

        if (quotation.Status != SalesQuotationStatus.Sent && quotation.Status != SalesQuotationStatus.Confirmed)
            throw new InvalidOperationException($"Cannot accept quotation with status '{quotation.Status}'. Only formally approved or issued quotations can be accepted.");

        if (quotation.IsExpired())
            throw new InvalidOperationException("Quotation has expired and cannot be accepted. Please request a formal quotation revision.");

        if (quotation.ConvertedSalesOrderId.HasValue || quotation.Status == SalesQuotationStatus.Converted)
            throw new InvalidOperationException("This quotation has already been converted into a Sales Order.");

        var now = DateTime.UtcNow;

        var tx = _persistenceCoordinator != null 
            ? await _persistenceCoordinator.BeginTransactionIfNeededAsync() 
            : (_dbContext != null ? await _dbContext.Database.BeginTransactionAsync() : null);

        try
        {
            // Step 1: Instantiate Official Sales Order Draft
            string orderNum = $"SO-QT-{quotation.QuotationNumber.Replace("SQ-", "")}";
            if (_salesOrderRepository != null && await _salesOrderRepository.ExistsOrderNumberAsync(tenantId, quotation.CompanyId, orderNum))
            {
                orderNum = $"SO-QT-{quotation.QuotationNumber.Replace("SQ-", "")}-{Guid.NewGuid():N[..4].ToUpperInvariant()}";
            }

            // Validate Delivery Address Ownership if a specific delivery address is requested during acceptance
            Guid? effectiveDeliveryAddressId = quotation.DeliveryAddressId;
            string? effectiveDeliveryAddressSnapshot = quotation.DeliveryAddressSnapshot;

            if (request?.DeliveryAddressId.HasValue == true && request.DeliveryAddressId.Value != Guid.Empty)
            {
                if (_dbContext != null)
                {
                    var isLinked = await _dbContext.CustomerAddresses
                        .AnyAsync(ca => ca.TenantId == tenantId && ca.CustomerId == customerId && ca.AddressId == request.DeliveryAddressId.Value && ca.IsActive);

                    if (!isLinked)
                    {
                        throw new InvalidOperationException($"The specified delivery address '{request.DeliveryAddressId.Value}' does not belong to this customer account or is inactive.");
                    }

                    var addr = await _dbContext.Addresses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AddressId == request.DeliveryAddressId.Value && a.IsActive);

                    if (addr != null)
                    {
                        effectiveDeliveryAddressSnapshot = $"{addr.AddressLine1}, {addr.City}".Trim(',', ' ');
                    }
                }

                effectiveDeliveryAddressId = request.DeliveryAddressId.Value;
            }

            var salesOrder = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
                tenantId,
                quotation.CompanyId,
                quotation.BranchId,
                customerId,
                orderNum,
                documentType: global::Domain.Features.Sales.SalesOrder.SalesOrderType.Standard,
                orderDate: now,
                deliveryDate: quotation.DeliveryDate,
                currency: quotation.Currency,
                paymentTerms: quotation.PaymentTerms ?? customer.PaymentTerms,
                notes: $"Created via acceptance of Quotation '{quotation.QuotationNumber}'" + (string.IsNullOrWhiteSpace(request?.CustomerRemarks) ? "" : $" | {request.CustomerRemarks.Trim()}"),
                createdByUserId: customerContactId,
                deliveryAddressId: effectiveDeliveryAddressId,
                deliveryAddressSnapshot: effectiveDeliveryAddressSnapshot
            );

            foreach (var item in quotation.Items.Where(i => i.IsActive))
            {
                salesOrder.AddItem(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountRate,
                    item.TaxRate,
                    item.UnitOfMeasureId,
                    item.Notes,
                    item.TaxCodeId,
                    item.IsTaxInclusive);
            }

            if (quotation.TransactionCurrencyId.HasValue && quotation.BaseCurrencyId.HasValue && quotation.AppliedExchangeRate.HasValue && quotation.ExchangeRateSource.HasValue)
            {
                var exRate = new ResolvedExchangeRate(
                    quotation.AppliedExchangeRate.Value,
                    quotation.ExchangeRateSource.Value,
                    quotation.ExchangeRateId,
                    quotation.ExchangeRateEffectiveDate);

                salesOrder.SetCurrencySnapshot(
                    quotation.TransactionCurrencyId.Value,
                    quotation.Currency,
                    quotation.BaseCurrencyId.Value,
                    quotation.BaseCurrencyCode ?? "USD",
                    quotation.BaseCurrencyDecimalPlaces ?? 2,
                    exRate);
            }

            if (_salesOrderRepository != null)
            {
                await _salesOrderRepository.AddAsync(salesOrder);
            }
            else if (_dbContext != null)
            {
                _dbContext.SalesOrders.Add(salesOrder);
            }

            // Step 2: Price Card Resolution, Creation & Update
            await ResolveOrUpdatePriceCardAsync(tenantId, customer, quotation, salesOrder, now);

            // Step 3: Quotation State Transitions
            quotation.Accept(customerContactId, now);
            quotation.MarkConverted(salesOrder.Id, now);

            if (_persistenceCoordinator != null)
            {
                await _persistenceCoordinator.SaveChangesAsync();
                await _persistenceCoordinator.CommitTransactionIfNeededAsync(tx);
            }
            else if (_dbContext != null)
            {
                await _dbContext.SaveChangesAsync();
                if (tx is Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction efTx)
                {
                    await efTx.CommitAsync();
                }
            }
            else
            {
                await _repository.SaveChangesAsync();
            }

            return MapToDto(quotation);
        }
        catch
        {
            if (_persistenceCoordinator != null)
            {
                await _persistenceCoordinator.RollbackTransactionIfNeededAsync(tx);
            }
            else if (tx is Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction efTx)
            {
                await efTx.RollbackAsync();
            }
            throw;
        }
    }

    public async Task<SalesQuotationDto> ConvertQuotationToOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId,
        Guid actorUserId,
        string actorUserName)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId)
            ?? throw new KeyNotFoundException($"Quotation with ID '{quotationId}' was not found in this operating company and branch.");

        if (quotation.Status != SalesQuotationStatus.Accepted || !quotation.AcceptedAtUtc.HasValue || !quotation.CustomerContactId.HasValue)
        {
            throw new InvalidOperationException($"Cannot convert quotation with status '{quotation.Status}'. Sales Order conversion is strictly gated on verified customer acceptance.");
        }

        if (quotation.IsExpired())
        {
            throw new InvalidOperationException("Quotation has expired and cannot be converted. Please prepare a revised quotation.");
        }

        if (quotation.ConvertedSalesOrderId.HasValue || quotation.Status == SalesQuotationStatus.Converted)
        {
            // Idempotent safe handling: if already converted, return existing mapped DTO
            var alreadyConvertedDto = MapToDto(quotation);
            await EnrichQuotationDtoAsync(alreadyConvertedDto, tenantId);
            return alreadyConvertedDto;
        }

        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, quotation.CustomerId)
            ?? throw new InvalidOperationException("Customer account is inactive or not found.");

        if (!customer.IsActive)
        {
            throw new InvalidOperationException($"Customer '{customer.CustomerCode}' is inactive.");
        }

        var now = DateTime.UtcNow;

        var tx = _persistenceCoordinator != null
            ? await _persistenceCoordinator.BeginTransactionIfNeededAsync()
            : (_dbContext != null ? await _dbContext.Database.BeginTransactionAsync() : null);

        try
        {
            // Step 1: Instantiate Official Sales Order Draft
            string orderNum = $"SO-QT-{quotation.QuotationNumber.Replace("SQ-", "")}";
            if (_salesOrderRepository != null && await _salesOrderRepository.ExistsOrderNumberAsync(tenantId, quotation.CompanyId, orderNum))
            {
                orderNum = $"SO-QT-{quotation.QuotationNumber.Replace("SQ-", "")}-{Guid.NewGuid():N[..4].ToUpperInvariant()}";
            }

            var salesOrder = global::Domain.Features.Sales.SalesOrder.SalesOrder.Create(
                tenantId,
                quotation.CompanyId,
                quotation.BranchId,
                quotation.CustomerId,
                orderNum,
                documentType: global::Domain.Features.Sales.SalesOrder.SalesOrderType.Standard,
                orderDate: now,
                deliveryDate: quotation.DeliveryDate,
                currency: quotation.Currency,
                paymentTerms: quotation.PaymentTerms ?? customer.PaymentTerms,
                notes: $"Created via conversion of Quotation '{quotation.QuotationNumber}' by {actorUserName}",
                createdByUserId: actorUserId,
                deliveryAddressId: quotation.DeliveryAddressId,
                deliveryAddressSnapshot: quotation.DeliveryAddressSnapshot
            );

            foreach (var item in quotation.Items.Where(i => i.IsActive))
            {
                salesOrder.AddItem(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountRate,
                    item.TaxRate,
                    item.UnitOfMeasureId,
                    item.Notes,
                    item.TaxCodeId,
                    item.IsTaxInclusive);
            }

            if (quotation.TransactionCurrencyId.HasValue && quotation.BaseCurrencyId.HasValue && quotation.AppliedExchangeRate.HasValue && quotation.ExchangeRateSource.HasValue)
            {
                var exRate = new ResolvedExchangeRate(
                    quotation.AppliedExchangeRate.Value,
                    quotation.ExchangeRateSource.Value,
                    quotation.ExchangeRateId,
                    quotation.ExchangeRateEffectiveDate);

                salesOrder.SetCurrencySnapshot(
                    quotation.TransactionCurrencyId.Value,
                    quotation.Currency,
                    quotation.BaseCurrencyId.Value,
                    quotation.BaseCurrencyCode ?? "USD",
                    quotation.BaseCurrencyDecimalPlaces ?? 2,
                    exRate);
            }

            if (_salesOrderRepository != null)
            {
                await _salesOrderRepository.AddAsync(salesOrder);
            }
            else if (_dbContext != null)
            {
                _dbContext.SalesOrders.Add(salesOrder);
            }

            // Step 2: Price Card Resolution, Creation & Update
            await ResolveOrUpdatePriceCardAsync(tenantId, customer, quotation, salesOrder, now);

            // Step 3: Transition quotation
            quotation.MarkConverted(salesOrder.Id, now);

            if (_persistenceCoordinator != null)
            {
                await _persistenceCoordinator.SaveChangesAsync();
                await _persistenceCoordinator.CommitTransactionIfNeededAsync(tx);
            }
            else if (_dbContext != null)
            {
                await _dbContext.SaveChangesAsync();
                if (tx is Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction efTx)
                {
                    await efTx.CommitAsync();
                }
            }
            else
            {
                await _repository.SaveChangesAsync();
            }

            return MapToDto(quotation);
        }
        catch
        {
            if (_persistenceCoordinator != null)
            {
                await _persistenceCoordinator.RollbackTransactionIfNeededAsync(tx);
            }
            else if (tx is Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction efTx)
            {
                await efTx.RollbackAsync();
            }
            throw;
        }
    }

    private async Task ResolveOrUpdatePriceCardAsync(
        Guid tenantId,
        Customer customer,
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation,
        global::Domain.Features.Sales.SalesOrder.SalesOrder salesOrder,
        DateTime now)
    {
        if (_priceCardRepository == null && _dbContext == null)
            return;

        PriceCard? suitablePriceCard = null;

        // 1. Check customer's default sales price card if assigned
        if (customer.DefaultSalesPriceCardId.HasValue)
        {
            var cardId = customer.DefaultSalesPriceCardId.Value;
            if (_priceCardRepository != null)
            {
                suitablePriceCard = await _priceCardRepository.GetPriceCardByIdAsync(tenantId, cardId, includeLines: true);
            }
            else if (_dbContext != null)
            {
                suitablePriceCard = await _dbContext.PriceCards
                    .Include(pc => pc.Lines)
                    .FirstOrDefaultAsync(pc => pc.TenantId == tenantId && pc.PriceCardId == cardId);
            }

            if (suitablePriceCard != null)
            {
                bool isSuitable = suitablePriceCard.IsActive
                    && suitablePriceCard.PriceCardType == PriceCardType.Sales
                    && string.Equals(suitablePriceCard.Currency, quotation.Currency, StringComparison.OrdinalIgnoreCase)
                    && (!suitablePriceCard.EffectiveTo.HasValue || suitablePriceCard.EffectiveTo.Value >= now)
                    && suitablePriceCard.EffectiveFrom <= now;

                if (!isSuitable)
                {
                    suitablePriceCard = null;
                }
            }
        }

        // 2. If default card is not suitable, search for an existing customer-specific Sales price card matching currency
        if (suitablePriceCard == null)
        {
            string expectedCustomerCardCode = $"SPC-{customer.CustomerCode}-{quotation.Currency}".ToUpperInvariant();
            if (_priceCardRepository != null)
            {
                var existingCustomerCard = await _priceCardRepository.GetPriceCardByCodeAsync(tenantId, expectedCustomerCardCode);
                if (existingCustomerCard != null
                    && existingCustomerCard.IsActive
                    && existingCustomerCard.PriceCardType == PriceCardType.Sales
                    && string.Equals(existingCustomerCard.Currency, quotation.Currency, StringComparison.OrdinalIgnoreCase)
                    && (!existingCustomerCard.EffectiveTo.HasValue || existingCustomerCard.EffectiveTo.Value >= now)
                    && existingCustomerCard.EffectiveFrom <= now)
                {
                    suitablePriceCard = existingCustomerCard;
                }
            }
            else if (_dbContext != null)
            {
                suitablePriceCard = await _dbContext.PriceCards
                    .Include(pc => pc.Lines)
                    .FirstOrDefaultAsync(pc =>
                        pc.TenantId == tenantId
                        && pc.PriceCardCode == expectedCustomerCardCode
                        && pc.PriceCardType == PriceCardType.Sales
                        && pc.IsActive
                        && (!pc.EffectiveTo.HasValue || pc.EffectiveTo.Value >= now)
                        && pc.EffectiveFrom <= now);
            }
        }

        // 3. If suitable price card exists, update or add applicable entries
        if (suitablePriceCard != null)
        {
            foreach (var item in quotation.Items.Where(i => i.IsActive))
            {
                var existingLine = suitablePriceCard.Lines.FirstOrDefault(l =>
                    l.ProductId == item.ProductId &&
                    l.UnitOfMeasureId == item.UnitOfMeasureId);

                if (existingLine != null)
                {
                    existingLine.Update(
                        item.UnitPrice,
                        item.UnitOfMeasureId,
                        item.Quantity,
                        now,
                        null);

                    if (!existingLine.IsActive)
                    {
                        existingLine.Activate();
                    }
                }
                else
                {
                    var newLine = suitablePriceCard.AddLine(
                        item.ProductId,
                        item.UnitPrice,
                        item.UnitOfMeasureId,
                        minimumQuantity: item.Quantity,
                        effectiveFrom: now,
                        effectiveTo: null);

                    if (_priceCardRepository != null)
                    {
                        await _priceCardRepository.AddLineAsync(newLine);
                    }
                    else if (_dbContext != null)
                    {
                        _dbContext.PriceCardLines.Add(newLine);
                    }
                }
            }

            if (!customer.DefaultSalesPriceCardId.HasValue)
            {
                customer.SetDefaultSalesPriceCard(suitablePriceCard.PriceCardId);
            }

            salesOrder.PriceCardId = suitablePriceCard.PriceCardId;
        }
        else
        {
            // 4. Create new Price Card with accepted terms
            string baseCode = $"SPC-{customer.CustomerCode}-{quotation.Currency}".ToUpperInvariant();
            string priceCardCode = baseCode;

            bool codeExists = _priceCardRepository != null
                ? await _priceCardRepository.ExistsByCodeAsync(tenantId, priceCardCode)
                : (_dbContext != null && await _dbContext.PriceCards.AnyAsync(pc => pc.TenantId == tenantId && pc.PriceCardCode == priceCardCode));

            if (codeExists)
            {
                priceCardCode = $"{baseCode}-{Guid.NewGuid():N[..4].ToUpperInvariant()}";
            }

            string priceCardName = $"Sales Price Card - {customer.CustomerName} ({quotation.Currency})";
            var newPriceCard = PriceCard.Create(
                tenantId,
                priceCardCode,
                priceCardName,
                PriceCardType.Sales,
                quotation.Currency,
                effectiveFrom: now,
                effectiveTo: null,
                description: $"Auto-generated upon acceptance of Sales Quotation {quotation.QuotationNumber}");

            foreach (var item in quotation.Items.Where(i => i.IsActive))
            {
                var line = newPriceCard.AddLine(
                    item.ProductId,
                    item.UnitPrice,
                    item.UnitOfMeasureId,
                    minimumQuantity: item.Quantity,
                    effectiveFrom: now,
                    effectiveTo: null);

                if (_priceCardRepository != null)
                {
                    await _priceCardRepository.AddLineAsync(line);
                }
                else if (_dbContext != null)
                {
                    _dbContext.PriceCardLines.Add(line);
                }
            }

            if (_priceCardRepository != null)
            {
                await _priceCardRepository.AddPriceCardAsync(newPriceCard);
            }
            else if (_dbContext != null)
            {
                _dbContext.PriceCards.Add(newPriceCard);
            }

            customer.SetDefaultSalesPriceCard(newPriceCard.PriceCardId);
            salesOrder.PriceCardId = newPriceCard.PriceCardId;
        }
    }

    public async Task<SalesQuotationDto> DeclineQuotationAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        Guid quotationId,
        DeclineQuotationRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Remarks))
            throw new ArgumentException("Remarks are required when declining a quotation.", nameof(request));

        var quotation = await _repository.GetByIdUnscopedAsync(tenantId, quotationId)
            ?? throw new KeyNotFoundException("Quotation not found in authorized customer account.");

        if (quotation.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to decline quotations belonging to another customer.");

        quotation.Decline(customerContactId, request.Remarks, DateTime.UtcNow);
        await _repository.SaveChangesAsync();
        return MapToDto(quotation);
    }

    public async Task<SalesQuotationDto> CloneCustomerQuotationRequestFromOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid customerContactId,
        Guid sourceSalesOrderId,
        string? customerRemarks = null)
    {
        global::Domain.Features.Sales.SalesOrder.SalesOrder? order = null;
        if (_salesOrderRepository != null)
        {
            order = await _salesOrderRepository.GetByIdAsync(tenantId, companyId, branchId, sourceSalesOrderId);
        }
        else if (_dbContext != null)
        {
            order = await _dbContext.SalesOrders.Include(so => so.Items).FirstOrDefaultAsync(so => so.TenantId == tenantId && so.Id == sourceSalesOrderId);
        }

        if (order == null || order.CustomerId != customerId)
            throw new KeyNotFoundException("Source Sales Order was not found in authorized customer account.");

        var items = new List<CreateSalesQuotationItemRequest>();
        foreach (var item in order.Items.Where(i => i.IsActive))
        {
            var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId);
            if (product != null && product.IsActive && product.IsSellable)
            {
                items.Add(new CreateSalesQuotationItemRequest
                {
                    ProductId = product.ProductId,
                    Quantity = item.Quantity,
                    UnitOfMeasureId = item.UnitOfMeasureId ?? product.UnitOfMeasureId,
                    UnitPrice = product.SellingPrice, // Never blindly copy historical price!
                    DiscountRate = 0m,
                    Notes = item.Notes
                });
            }
        }

        if (items.Count == 0)
            throw new InvalidOperationException("None of the products on the source order are currently active and sellable.");

        var createReq = new CreateCustomerQuotationRequest
        {
            CustomerId = customerId,
            CustomerContactId = customerContactId,
            Currency = order.Currency,
            CustomerRemarks = $"Cloned from Order '{order.OrderNumber}'" + (string.IsNullOrWhiteSpace(customerRemarks) ? "" : $" | {customerRemarks}"),
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Items = items
        };

        return await CreateCustomerQuotationRequestAsync(tenantId, companyId, branchId, createReq, customerContactId, "B2B Customer");
    }

    public async Task<SalesQuotationDto> CloneCustomerQuotationRequestFromQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        Guid customerContactId,
        Guid sourceSalesQuotationId,
        string? customerRemarks = null)
    {
        var sourceQuotation = await _repository.GetByIdUnscopedAsync(tenantId, sourceSalesQuotationId);
        if (sourceQuotation == null || sourceQuotation.CustomerId != customerId)
            throw new KeyNotFoundException("Source Sales Quotation was not found in authorized customer account.");

        var items = new List<CreateSalesQuotationItemRequest>();
        foreach (var item in sourceQuotation.Items.Where(i => i.IsActive))
        {
            var product = await _productRepository.GetByIdAsync(tenantId, item.ProductId);
            if (product != null && product.IsActive && product.IsSellable)
            {
                items.Add(new CreateSalesQuotationItemRequest
                {
                    ProductId = product.ProductId,
                    Quantity = item.Quantity,
                    UnitOfMeasureId = item.UnitOfMeasureId ?? product.UnitOfMeasureId,
                    UnitPrice = product.SellingPrice, // Never blindly copy historical price!
                    DiscountRate = 0m,
                    Notes = item.Notes
                });
            }
        }

        if (items.Count == 0)
            throw new InvalidOperationException("None of the products on the source quotation are currently active and sellable.");

        var createReq = new CreateCustomerQuotationRequest
        {
            CustomerId = customerId,
            CustomerContactId = customerContactId,
            Currency = sourceQuotation.Currency,
            CustomerRemarks = $"Cloned from Quotation '{sourceQuotation.QuotationNumber}'" + (string.IsNullOrWhiteSpace(customerRemarks) ? "" : $" | {customerRemarks}"),
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Items = items
        };

        return await CreateCustomerQuotationRequestAsync(tenantId, companyId, branchId, createReq, customerContactId, "B2B Customer");
    }

    public async Task<CustomerSalesQuotationDto?> GetCustomerQuotationDetailsAsync(
        Guid tenantId,
        Guid customerId,
        Guid quotationId)
    {
        var quotation = await _repository.GetByIdUnscopedAsync(tenantId, quotationId);
        if (quotation == null || quotation.CustomerId != customerId)
            return null;

        int daysLeft = (int)Math.Max(0, (quotation.ValidUntilDateUtc - DateTime.UtcNow).TotalDays);

        Guid? linkedCustomerRequestId = null;
        string? linkedCustomerRequestNumber = null;
        Guid? linkedCommercialQuotationId = null;
        string? linkedCommercialQuotationNumber = null;
        string? requestReviewStatus = null;

        if (!string.IsNullOrWhiteSpace(quotation.InternalNotes))
        {
            var matchReq = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[LinkedCustomerRequestId:([0-9a-fA-F-]+)\|RequestNumber:([^\]]+)\]");
            if (matchReq.Success && Guid.TryParse(matchReq.Groups[1].Value, out var parsedReqId))
            {
                linkedCustomerRequestId = parsedReqId;
                linkedCustomerRequestNumber = matchReq.Groups[2].Value.Trim();
            }

            var matchCom = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[LinkedCommercialQuotationId:([0-9a-fA-F-]+)\|QuotationNumber:([^\]]+)\]");
            if (matchCom.Success && Guid.TryParse(matchCom.Groups[1].Value, out var parsedComId))
            {
                linkedCommercialQuotationId = parsedComId;
                linkedCommercialQuotationNumber = matchCom.Groups[2].Value.Trim();
            }

            var matchRev = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[RequestReviewStatus:([^\]]+)\]");
            if (matchRev.Success)
            {
                requestReviewStatus = matchRev.Groups[1].Value.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(requestReviewStatus) && quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            if (linkedCommercialQuotationId.HasValue)
            {
                requestReviewStatus = "QuotationPrepared";
            }
            else if (quotation.Status == SalesQuotationStatus.Draft)
            {
                requestReviewStatus = "UnderReview";
            }
            else if (quotation.Status == SalesQuotationStatus.Submitted)
            {
                requestReviewStatus = "Submitted";
            }
            else if (quotation.Status == SalesQuotationStatus.Cancelled)
            {
                requestReviewStatus = "Closed";
            }
            else
            {
                requestReviewStatus = "Submitted";
            }
        }

        var customerDto = new CustomerSalesQuotationDto
        {
            Id = quotation.Id,
            TenantId = quotation.TenantId,
            CompanyId = quotation.CompanyId,
            BranchId = quotation.BranchId,
            CustomerId = quotation.CustomerId,
            QuotationNumber = quotation.QuotationNumber,
            RevisionNumber = quotation.RevisionNumber,
            DocumentType = quotation.DocumentType,
            QuotationDate = quotation.QuotationDate,
            ValidUntilDateUtc = quotation.ValidUntilDateUtc,
            IsExpired = quotation.IsExpired(),
            DaysUntilExpiry = daysLeft,
            DeliveryDate = quotation.DeliveryDate,
            Currency = quotation.Currency,
            PaymentTerms = quotation.PaymentTerms,
            DeliveryTerms = quotation.DeliveryTerms,
            Status = quotation.Status,
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount,
            Notes = quotation.Notes,
            CustomerRemarks = CleanCustomerRemarks(quotation.CustomerRemarks),
            SentAtUtc = quotation.SentAtUtc,
            AcceptedAtUtc = quotation.AcceptedAtUtc,
            DeclinedAtUtc = quotation.DeclinedAtUtc,
            DeclinedRemarks = quotation.DeclinedRemarks,
            ConvertedSalesOrderId = quotation.ConvertedSalesOrderId,
            ConvertedAtUtc = quotation.ConvertedAtUtc,
            LinkedCustomerRequestId = linkedCustomerRequestId,
            LinkedCustomerRequestNumber = linkedCustomerRequestNumber,
            LinkedCommercialQuotationId = linkedCommercialQuotationId,
            LinkedCommercialQuotationNumber = linkedCommercialQuotationNumber,
            RequestReviewStatus = requestReviewStatus,
            Items = quotation.Items.Where(i => i.IsActive).Select(MapItemToDto).ToList()
        };

        if (_dbContext != null && customerDto.Items.Count > 0)
        {
            var productIds = customerDto.Items.Select(i => i.ProductId).Distinct().ToList();
            var uomIds = customerDto.Items.Where(i => i.UnitOfMeasureId.HasValue).Select(i => i.UnitOfMeasureId!.Value).Distinct().ToList();

            var products = await _dbContext.Products
                .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId);

            var uoms = await _dbContext.UnitsOfMeasure
                .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UnitOfMeasureId))
                .ToDictionaryAsync(u => u.UnitOfMeasureId);

            foreach (var item in customerDto.Items)
            {
                if (products.TryGetValue(item.ProductId, out var prod))
                {
                    item.ProductCode = prod.ProductCode;
                    item.ProductName = prod.ProductName;
                }
                if (item.UnitOfMeasureId.HasValue && uoms.TryGetValue(item.UnitOfMeasureId.Value, out var uom))
                {
                    item.UnitOfMeasureCode = uom.Code;
                }
                else if (string.IsNullOrWhiteSpace(item.UnitOfMeasureCode))
                {
                    item.UnitOfMeasureCode = prod != null && !string.IsNullOrWhiteSpace(prod.UnitOfMeasure) ? prod.UnitOfMeasure : "EA";
                }
            }
        }

        return customerDto;
    }

    public async Task<byte[]> GenerateQuotationPdfAsync(Guid tenantId, Guid quotationId)
    {
        if (_pdfService == null)
            throw new InvalidOperationException("PDF generation service is unavailable.");

        return await _pdfService.GeneratePdfForQuotationAsync(tenantId, quotationId);
    }

    private async Task<(Product product, Guid? uomId, decimal unitPrice)> ValidateAndResolveItemAsync(
        Guid tenantId,
        CreateSalesQuotationItemRequest itemReq)
    {
        var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product '{itemReq.ProductId}' was not found or does not belong to this tenant.");
        }

        if (!product.IsActive)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' is inactive.");
        }

        if (!product.IsSellable)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not marked as sellable.");
        }

        Guid? uomId = itemReq.UnitOfMeasureId ?? product.UnitOfMeasureId;
        if (uomId.HasValue)
        {
            var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
            if (uom == null || !uom.IsActive)
            {
                throw new InvalidOperationException($"Unit of Measure '{uomId.Value}' is invalid or inactive.");
            }
        }

        decimal unitPrice = itemReq.UnitPrice;
        if (unitPrice <= 0 && product.SellingPrice > 0)
        {
            unitPrice = product.SellingPrice;
        }

        return (product, uomId, unitPrice);
    }

    private async Task<decimal> ResolveTaxRateAsync(Guid tenantId, Guid? taxCodeId, decimal explicitTaxRate, DateTime orderDate)
    {
        if (taxCodeId.HasValue && _taxCodeRepository != null)
        {
            var taxCode = await _taxCodeRepository.GetTaxCodeByIdAsync(tenantId, taxCodeId.Value);
            if (taxCode == null)
            {
                throw new InvalidOperationException($"Tax code '{taxCodeId.Value}' was not found or does not belong to this tenant.");
            }
            if (!taxCode.IsActive)
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' is inactive.");
            }
            if (!taxCode.AppliesTo(TaxApplicability.Sales))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' cannot be applied to sales transactions.");
            }
            if (!taxCode.IsEffectiveAt(orderDate))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' is not effective on {orderDate:yyyy-MM-dd}.");
            }
            return taxCode.Rate;
        }

        return explicitTaxRate;
    }

    private async Task ApplyCurrencySnapshotAsync(
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation,
        string transactionCurrencyCode)
    {
        if (_currencyRepository == null || _exchangeRateService == null || _persistenceCoordinator == null)
            return;

        try
        {
            var transactionCurrency = await _currencyRepository.GetCurrencyByCodeAsync(quotation.TenantId, transactionCurrencyCode);
            if (transactionCurrency == null || !transactionCurrency.IsActive)
                return;

            var baseCurrency = await _persistenceCoordinator.GetActiveCompanyBaseCurrencyAsync(quotation.TenantId, quotation.CompanyId);
            var rate = await _exchangeRateService.ResolveAsync(
                quotation.TenantId,
                transactionCurrency.CurrencyId,
                baseCurrency.CurrencyId,
                quotation.QuotationDate);

            quotation.SetCurrencySnapshot(
                transactionCurrency.CurrencyId,
                baseCurrency.CurrencyId,
                baseCurrency.CurrencyCode,
                baseCurrency.DecimalPlaces,
                rate.Rate,
                rate.Source,
                rate.ExchangeRateId,
                rate.EffectiveDate);
        }
        catch
        {
            // Currency snapshot resolution graceful fallback if exchange rate is not configured
        }
    }

    private async Task EnrichQuotationDtoAsync(SalesQuotationDto dto, Guid tenantId)
    {
        if (dto == null) return;

        if (dto.CustomerId != Guid.Empty && string.IsNullOrEmpty(dto.CustomerCode) && _customerRepository != null)
        {
            try
            {
                var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, dto.CustomerId);
                if (customer != null)
                {
                    dto.CustomerCode = customer.CustomerCode;
                    dto.CustomerName = customer.CustomerName;
                }
            }
            catch { }
        }

        if (dto.CustomerContactId.HasValue && dto.CustomerContactId.Value != Guid.Empty && _dbContext != null)
        {
            try
            {
                var contact = await _dbContext.Contacts
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContactId == dto.CustomerContactId.Value);

                if (contact == null)
                {
                    var custContact = await _dbContext.CustomerContacts
                        .FirstOrDefaultAsync(cc => cc.TenantId == tenantId && cc.CustomerContactId == dto.CustomerContactId.Value);
                    if (custContact != null)
                    {
                        contact = await _dbContext.Contacts
                            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContactId == custContact.ContactId);
                    }
                }

                if (contact != null)
                {
                    dto.ContactPersonName = contact.ContactName;
                    dto.ContactEmail = contact.Email;
                }
            }
            catch { }
        }

        if (_dbContext != null)
        {
            var itemProductIds = dto.Items.Select(i => i.ProductId).ToList();
            if (dto.OriginalRequest?.Items != null)
            {
                itemProductIds.AddRange(dto.OriginalRequest.Items.Select(oi => oi.ProductId));
            }
            var distinctProductIds = itemProductIds.Distinct().ToList();

            var uomIds = dto.Items.Where(i => i.UnitOfMeasureId.HasValue).Select(i => i.UnitOfMeasureId!.Value).Distinct().ToList();

            var products = await _dbContext.Products
                .Where(p => p.TenantId == tenantId && distinctProductIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId);

            var uoms = await _dbContext.UnitsOfMeasure
                .Where(u => u.TenantId == tenantId && uomIds.Contains(u.UnitOfMeasureId))
                .ToDictionaryAsync(u => u.UnitOfMeasureId);

            decimal totalCost = 0;
            bool hasAnyCost = false;

            foreach (var item in dto.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    item.ProductCode = product.ProductCode;
                    item.ProductName = product.ProductName;
                    item.UnitCost = product.StandardCost;

                    if (product.StandardCost > 0)
                    {
                        hasAnyCost = true;
                        if (item.UnitPrice > 0)
                        {
                            item.GrossMarginPercentage = Math.Round(((item.UnitPrice - product.StandardCost) / item.UnitPrice) * 100m, 2);
                        }
                        totalCost += product.StandardCost * item.Quantity;
                    }
                }

                if (item.UnitOfMeasureId.HasValue && uoms.TryGetValue(item.UnitOfMeasureId.Value, out var uom))
                {
                    item.UnitOfMeasureCode = uom.Code;
                }
            }

            if (dto.OriginalRequest?.Items != null)
            {
                foreach (var origItem in dto.OriginalRequest.Items)
                {
                    if (products.TryGetValue(origItem.ProductId, out var p))
                    {
                        origItem.ProductCode = p.ProductCode;
                        origItem.ProductName = p.ProductName;
                        if (string.IsNullOrWhiteSpace(origItem.UnitOfMeasureCode))
                        {
                            origItem.UnitOfMeasureCode = !string.IsNullOrWhiteSpace(p.UnitOfMeasure) ? p.UnitOfMeasure : "EA";
                        }
                    }
                }
            }

            if (hasAnyCost)
            {
                dto.TotalCost = Math.Round(totalCost, 2);
                if (dto.SubTotal > 0)
                {
                    dto.GrossMarginPercentage = Math.Round(((dto.SubTotal - totalCost) / dto.SubTotal) * 100m, 2);
                }
            }
        }
    }

    private static SalesQuotationDto MapToDto(global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation)
    {
        OriginalCustomerRequestDto? originalRequest = null;
        if (!string.IsNullOrWhiteSpace(quotation.InternalNotes) && quotation.InternalNotes.Contains("[ORIGINAL_REQUEST_SNAPSHOT:"))
        {
            try
            {
                int startIdx = quotation.InternalNotes.IndexOf("[ORIGINAL_REQUEST_SNAPSHOT:", StringComparison.Ordinal);
                if (startIdx >= 0)
                {
                    int jsonStart = startIdx + "[ORIGINAL_REQUEST_SNAPSHOT:".Length;
                    int endIdx = quotation.InternalNotes.IndexOf("]", jsonStart, StringComparison.Ordinal);
                    if (endIdx > jsonStart)
                    {
                        var json = quotation.InternalNotes.Substring(jsonStart, endIdx - jsonStart);
                        originalRequest = System.Text.Json.JsonSerializer.Deserialize<OriginalCustomerRequestDto>(json);
                        if (originalRequest != null)
                        {
                            originalRequest.CustomerRemarks = CleanCustomerRemarks(originalRequest.CustomerRemarks);
                        }
                    }
                }
            }
            catch
            {
                // Graceful fallback
            }
        }

        if (originalRequest == null && quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            originalRequest = new OriginalCustomerRequestDto
            {
                CustomerRemarks = CleanCustomerRemarks(quotation.CustomerRemarks),
                DeliveryAddressSnapshot = quotation.DeliveryAddressSnapshot,
                RequiredDeliveryDate = quotation.DeliveryDate,
                Items = quotation.Items.Where(i => i.IsActive).Select(i => new OriginalCustomerRequestItemDto
                {
                    ProductId = i.ProductId,
                    RequestedQuantity = i.Quantity,
                    TargetUnitPrice = i.UnitPrice,
                    Notes = i.Notes
                }).ToList()
            };
        }

        string? emailStatus = null;
        string? emailRecipient = null;
        string? emailFailureReason = null;
        DateTime? emailSentAtUtc = quotation.SentAtUtc;

        if (!string.IsNullOrWhiteSpace(quotation.InternalNotes))
        {
            if (quotation.InternalNotes.Contains("[EmailStatus:Sent"))
            {
                emailStatus = "Sent";
                var match = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[EmailStatus:Sent\|Recipient:([^\|\]]+)");
                if (match.Success) emailRecipient = match.Groups[1].Value.Trim();
            }
            else if (quotation.InternalNotes.Contains("[EmailStatus:Pending"))
            {
                emailStatus = "Pending";
                var match = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[EmailStatus:Pending\|Recipient:([^\|\]]+)");
                if (match.Success) emailRecipient = match.Groups[1].Value.Trim();
            }
            else if (quotation.InternalNotes.Contains("[EmailStatus:Failed"))
            {
                emailStatus = "Failed";
                var matchRecipient = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[EmailStatus:Failed\|Recipient:([^\|\]]+)");
                if (matchRecipient.Success) emailRecipient = matchRecipient.Groups[1].Value.Trim();
                var matchError = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\|Error:([^\]]+)\]");
                if (matchError.Success) emailFailureReason = matchError.Groups[1].Value.Trim();
            }
        }

        if (string.IsNullOrEmpty(emailStatus))
        {
            emailStatus = quotation.SentAtUtc.HasValue ? "Sent" : "NotSent";
        }

        string approvalStatus = ResolveApprovalStatus(quotation);
        string customerStatus = ResolveCustomerStatus(quotation, emailStatus);

        Guid? linkedCustomerRequestId = null;
        string? linkedCustomerRequestNumber = null;
        Guid? linkedCommercialQuotationId = null;
        string? linkedCommercialQuotationNumber = null;
        string? requestReviewStatus = null;

        if (!string.IsNullOrWhiteSpace(quotation.InternalNotes))
        {
            var matchReq = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[LinkedCustomerRequestId:([0-9a-fA-F-]+)\|RequestNumber:([^\]]+)\]");
            if (matchReq.Success && Guid.TryParse(matchReq.Groups[1].Value, out var parsedReqId))
            {
                linkedCustomerRequestId = parsedReqId;
                linkedCustomerRequestNumber = matchReq.Groups[2].Value.Trim();
            }

            var matchCom = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[LinkedCommercialQuotationId:([0-9a-fA-F-]+)\|QuotationNumber:([^\]]+)\]");
            if (matchCom.Success && Guid.TryParse(matchCom.Groups[1].Value, out var parsedComId))
            {
                linkedCommercialQuotationId = parsedComId;
                linkedCommercialQuotationNumber = matchCom.Groups[2].Value.Trim();
            }

            var matchRev = System.Text.RegularExpressions.Regex.Match(quotation.InternalNotes, @"\[RequestReviewStatus:([^\]]+)\]");
            if (matchRev.Success)
            {
                requestReviewStatus = matchRev.Groups[1].Value.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(requestReviewStatus) && quotation.DocumentType == SalesQuotationType.CustomerRequest)
        {
            if (linkedCommercialQuotationId.HasValue)
            {
                requestReviewStatus = "QuotationPrepared";
            }
            else if (quotation.Status == SalesQuotationStatus.Draft)
            {
                requestReviewStatus = "UnderReview";
            }
            else if (quotation.Status == SalesQuotationStatus.Submitted)
            {
                requestReviewStatus = "Submitted";
            }
            else if (quotation.Status == SalesQuotationStatus.Cancelled)
            {
                requestReviewStatus = "Closed";
            }
            else
            {
                requestReviewStatus = "Submitted";
            }
        }

        return new SalesQuotationDto
        {
            Id = quotation.Id,
            TenantId = quotation.TenantId,
            CompanyId = quotation.CompanyId,
            BranchId = quotation.BranchId,
            CustomerId = quotation.CustomerId,
            CustomerContactId = quotation.CustomerContactId,
            CreatedByUserId = quotation.CreatedByUserId,
            QuotationNumber = quotation.QuotationNumber,
            RevisionNumber = quotation.RevisionNumber,
            DocumentType = quotation.DocumentType,
            QuotationDate = quotation.QuotationDate,
            ValidUntilDateUtc = quotation.ValidUntilDateUtc,
            DeliveryDate = quotation.DeliveryDate,
            Currency = quotation.Currency,
            TransactionCurrencyId = quotation.TransactionCurrencyId,
            BaseCurrencyId = quotation.BaseCurrencyId,
            BaseCurrencyCode = quotation.BaseCurrencyCode,
            BaseCurrencyDecimalPlaces = quotation.BaseCurrencyDecimalPlaces,
            AppliedExchangeRate = quotation.AppliedExchangeRate,
            PriceCardId = quotation.PriceCardId,
            PaymentTerms = quotation.PaymentTerms,
            DeliveryTerms = quotation.DeliveryTerms,
            Status = quotation.Status,
            ApprovalStatus = approvalStatus,
            CustomerStatus = customerStatus,
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount,
            BaseSubTotal = quotation.BaseSubTotal,
            BaseDiscountAmount = quotation.BaseDiscountAmount,
            BaseTaxAmount = quotation.BaseTaxAmount,
            BaseTotalAmount = quotation.BaseTotalAmount,
            Notes = quotation.Notes,
            CustomerRemarks = CleanCustomerRemarks(quotation.CustomerRemarks),
            InternalNotes = quotation.InternalNotes,
            DeliveryAddressId = quotation.DeliveryAddressId,
            DeliveryAddressSnapshot = quotation.DeliveryAddressSnapshot,
            SubmittedByUserId = quotation.SubmittedByUserId,
            SubmittedAtUtc = quotation.SubmittedAtUtc,
            ApprovedByUserId = quotation.ApprovedByUserId,
            ApprovedAtUtc = quotation.ApprovedAtUtc,
            RejectedByUserId = quotation.RejectedByUserId,
            RejectedAtUtc = quotation.RejectedAtUtc,
            RejectionRemarks = quotation.RejectionRemarks,
            SentAtUtc = quotation.SentAtUtc,
            AcceptedAtUtc = quotation.AcceptedAtUtc,
            DeclinedAtUtc = quotation.DeclinedAtUtc,
            DeclinedRemarks = quotation.DeclinedRemarks,
            ConvertedSalesOrderId = quotation.ConvertedSalesOrderId,
            ConvertedAtUtc = quotation.ConvertedAtUtc,
            SupersededByQuotationId = quotation.SupersededByQuotationId,
            LinkedCustomerRequestId = linkedCustomerRequestId,
            LinkedCustomerRequestNumber = linkedCustomerRequestNumber,
            LinkedCommercialQuotationId = linkedCommercialQuotationId,
            LinkedCommercialQuotationNumber = linkedCommercialQuotationNumber,
            RequestReviewStatus = requestReviewStatus,
            OriginalRequest = originalRequest,
            EmailStatus = emailStatus,
            EmailSentAtUtc = emailSentAtUtc,
            EmailRecipient = emailRecipient,
            EmailFailureReason = emailFailureReason,
            Items = quotation.Items.Where(i => i.IsActive).Select(MapItemToDto).ToList()
        };
    }

    private static string ResolveApprovalStatus(global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation)
    {
        return quotation.Status switch
        {
            SalesQuotationStatus.Draft => "Draft",
            SalesQuotationStatus.Submitted => "Pending Approval",
            SalesQuotationStatus.Confirmed => "Approved",
            SalesQuotationStatus.RejectedInternal => "Approval Rejected",
            SalesQuotationStatus.Sent => "Approved",
            SalesQuotationStatus.Accepted => "Approved",
            SalesQuotationStatus.DeclinedByCustomer => "Approved",
            SalesQuotationStatus.Converted => "Approved",
            SalesQuotationStatus.Cancelled => "Cancelled",
            SalesQuotationStatus.Superseded => quotation.ApprovedAtUtc.HasValue
                ? "Approved"
                : (quotation.RejectedAtUtc.HasValue ? "Approval Rejected" : "Superseded"),
            SalesQuotationStatus.Expired => quotation.ApprovedAtUtc.HasValue
                ? "Approved"
                : (quotation.RejectedAtUtc.HasValue
                    ? "Approval Rejected"
                    : (quotation.SubmittedAtUtc.HasValue ? "Pending Approval" : "Expired")),
            _ => "Draft"
        };
    }

    private static string ResolveCustomerStatus(
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation,
        string? emailStatus)
    {
        if (quotation.Status == SalesQuotationStatus.Converted || quotation.ConvertedSalesOrderId.HasValue)
            return "Sales Order Created";

        return quotation.Status switch
        {
            SalesQuotationStatus.Accepted => "Customer Accepted",
            SalesQuotationStatus.DeclinedByCustomer => "Customer Rejected",
            SalesQuotationStatus.Sent => "Sent to Customer",
            SalesQuotationStatus.Cancelled => "Cancelled",
            SalesQuotationStatus.Expired => "Expired",
            SalesQuotationStatus.Superseded => "Superseded",
            _ => string.Equals(emailStatus, "Pending", StringComparison.OrdinalIgnoreCase)
                ? "Pending Send"
                : (quotation.SentAtUtc.HasValue ? "Sent to Customer" : "Not Sent")
        };
    }

    private static string? CleanCustomerRemarks(string? remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks)) return remarks;
        var cleaned = System.Text.RegularExpressions.Regex.Replace(remarks, @"\[IdempotencyKey:[^\]]+\]", string.Empty);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*\|\s*\|+", " | ").Trim();
        cleaned = cleaned.Trim(' ', '|', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static SalesQuotationItemDto MapItemToDto(SalesQuotationItem item)
    {
        return new SalesQuotationItemDto
        {
            Id = item.Id,
            SalesQuotationId = item.SalesQuotationId,
            ProductId = item.ProductId,
            UnitOfMeasureId = item.UnitOfMeasureId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountRate = item.DiscountRate,
            DiscountAmount = item.DiscountAmount,
            TaxCodeId = item.TaxCodeId,
            TaxRate = item.TaxRate,
            TaxAmount = item.TaxAmount,
            SubTotal = item.SubTotal,
            LineTotal = item.LineTotal,
            IsTaxInclusive = item.IsTaxInclusive,
            Notes = item.Notes,
            IsActive = item.IsActive
        };
    }
}
