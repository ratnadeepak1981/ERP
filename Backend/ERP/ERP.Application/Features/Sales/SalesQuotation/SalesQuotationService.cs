using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.Approval;
using Domain.Features.Sales.SalesQuotation;
using ERP.Domain.Features.MasterData.Customer;

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

    public SalesQuotationService(
        ISalesQuotationRepository repository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IUnitOfMeasureRepository uomRepository,
        IApprovalWorkflowService approvalService,
        ITaxCodeRepository? taxCodeRepository = null,
        ICurrencyRepository? currencyRepository = null,
        IExchangeRateService? exchangeRateService = null,
        ISalesPersistenceCoordinator? persistenceCoordinator = null)
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
    }

    public async Task<List<SalesQuotationDto>> GetQuotationsAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesQuotationStatus? status = null)
    {
        var quotations = await _repository.GetByBranchAsync(tenantId, companyId, branchId, status);
        return quotations.Select(MapToDto).ToList();
    }

    public async Task<SalesQuotationDto?> GetQuotationAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId)
    {
        var quotation = await _repository.GetByIdAsync(tenantId, companyId, branchId, quotationId);
        return quotation == null ? null : MapToDto(quotation);
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
        if (request.InternalNotes != null) quotation.InternalNotes = request.InternalNotes;
        if (request.DeliveryAddressId.HasValue) quotation.DeliveryAddressId = request.DeliveryAddressId.Value;
        if (request.DeliveryAddressSnapshot != null) quotation.DeliveryAddressSnapshot = request.DeliveryAddressSnapshot;

        if (!string.IsNullOrWhiteSpace(request.Currency) && !string.Equals(quotation.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            quotation.Currency = request.Currency.Trim().ToUpperInvariant();
            await ApplyCurrencySnapshotAsync(quotation, quotation.Currency);
        }

        quotation.RecalculateTotals();
        await _repository.SaveChangesAsync();

        return MapToDto(quotation);
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
        return MapToDto(quotation);
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

        // Enforce approval record & self-approval guard via ApprovalWorkflowService
        await _approvalService.RecordApprovalAsync(
            tenantId,
            "SalesQuotation",
            quotation.Id,
            approverUserId,
            approverUserName,
            quotation.CreatedByUserId ?? Guid.Empty,
            quotation.TotalAmount,
            remarks);

        // Transition domain state: Submitted -> Confirmed
        quotation.Approve(approverUserId);

        await _repository.SaveChangesAsync();
        return MapToDto(quotation);
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

        // Record in approval audit log
        await _approvalService.RecordRejectionAsync(
            tenantId,
            "SalesQuotation",
            quotation.Id,
            approverUserId,
            approverUserName,
            remarks,
            quotation.TotalAmount);

        // Transition domain state: Submitted -> RejectedInternal
        quotation.Reject(approverUserId, remarks);

        await _repository.SaveChangesAsync();
        return MapToDto(quotation);
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
        return MapToDto(quotation);
    }

    public async Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid quotationId)
    {
        return await _approvalService.GetApprovalHistoryAsync(tenantId, "SalesQuotation", quotationId);
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

    private static SalesQuotationDto MapToDto(global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation)
    {
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
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.TotalAmount,
            BaseSubTotal = quotation.BaseSubTotal,
            BaseDiscountAmount = quotation.BaseDiscountAmount,
            BaseTaxAmount = quotation.BaseTaxAmount,
            BaseTotalAmount = quotation.BaseTotalAmount,
            Notes = quotation.Notes,
            CustomerRemarks = quotation.CustomerRemarks,
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
            Items = quotation.Items.Where(i => i.IsActive).Select(MapItemToDto).ToList()
        };
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
