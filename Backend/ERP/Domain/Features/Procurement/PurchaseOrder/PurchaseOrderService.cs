using System.Linq;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.PaymentTerm;
using Domain.Features.MasterData.Supplier;
using Domain.Features.Procurement.Approval;
using ERP.Domain.Features.MasterData.Supplier;

namespace Domain.Features.Procurement.PurchaseOrder;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierProductPriceService _supplierPriceService;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly IApprovalWorkflowService _approvalService;
    private readonly ITaxCodeRepository? _taxCodeRepository;
    private readonly DomainDbContext? _context;
    private readonly IUnitOfMeasureService? _uomService;
    private readonly ICurrencyRepository? _currencyRepository;
    private readonly IPaymentTermRepository? _paymentTermRepository;
    private readonly IExchangeRateService? _exchangeRateService;
    private readonly IPriceCardRepository? _priceCardRepository;
    private readonly IPricingResolutionService? _pricingResolutionService;

    public PurchaseOrderService(
        IPurchaseOrderRepository repository,
        IProductRepository productRepository,
        ISupplierRepository supplierRepository,
        ISupplierProductPriceService supplierPriceService,
        IUnitOfMeasureRepository uomRepository,
        IApprovalWorkflowService approvalService,
        ITaxCodeRepository? taxCodeRepository = null,
        DomainDbContext? context = null,
        IUnitOfMeasureService? uomService = null,
        ICurrencyRepository? currencyRepository = null,
        IPaymentTermRepository? paymentTermRepository = null,
        IExchangeRateService? exchangeRateService = null,
        IPriceCardRepository? priceCardRepository = null,
        IPricingResolutionService? pricingResolutionService = null)
    {
        _repository = repository;
        _productRepository = productRepository;
        _supplierRepository = supplierRepository;
        _supplierPriceService = supplierPriceService;
        _uomRepository = uomRepository;
        _approvalService = approvalService;
        _taxCodeRepository = taxCodeRepository;
        _context = context;
        _uomService = uomService;
        _currencyRepository = currencyRepository;
        _paymentTermRepository = paymentTermRepository;
        _exchangeRateService = exchangeRateService;
        _priceCardRepository = priceCardRepository;
        _pricingResolutionService = pricingResolutionService;
    }

    public async Task<List<PurchaseOrder>> GetOrdersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        var orders = await _repository.GetByBranchAsync(
            tenantId,
            companyId,
            branchId);
        await PopulateUnitOfMeasureCodesAsync(tenantId, orders);
        return orders;
    }

    public async Task<List<PurchaseOrder>> GetOrdersByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        IEnumerable<Guid>? branchIds = null)
    {
        var orders = await _repository.GetByCompanyAsync(
            tenantId,
            companyId,
            branchIds);
        await PopulateUnitOfMeasureCodesAsync(tenantId, orders);
        return orders;
    }

    public async Task<PurchaseOrder?> GetOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var order = await _repository.GetByIdAsync(
            tenantId,
            companyId,
            branchId,
            orderId);
        if (order != null)
            await PopulateUnitOfMeasureCodesAsync(tenantId, new[] { order });
        return order;
    }

    public async Task<PurchaseOrder?> GetOrderByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid orderId)
    {
        var order = await _repository.GetByIdAndCompanyAsync(
            tenantId,
            companyId,
            orderId);
        if (order != null)
            await PopulateUnitOfMeasureCodesAsync(tenantId, new[] { order });
        return order;
    }

    public async Task<PurchaseOrder> CreateOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreatePurchaseOrderRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.OrderNumber))
            throw new ArgumentException("Order number is required.", nameof(request.OrderNumber));

        string orderNumber = request.OrderNumber.Trim();

        if (await _repository.ExistsOrderNumberAsync(tenantId, companyId, orderNumber))
        {
            throw new InvalidOperationException(
                $"Purchase Order '{orderNumber}' already exists for this company.");
        }

        // Validate supplier if provided
        ERP.Domain.Features.MasterData.Supplier.Supplier? supplier = null;
        if (request.SupplierId.HasValue)
        {
            supplier = await _supplierRepository.GetSupplierByIdAsync(tenantId, request.SupplierId.Value);
            if (supplier == null)
            {
                throw new InvalidOperationException($"Supplier '{request.SupplierId.Value}' was not found or does not belong to this tenant.");
            }
        }

        var order = PurchaseOrder.Create(
            tenantId,
            companyId,
            branchId,
            orderNumber,
            request.OrderDate,
            request.SupplierId,
            actorUserId);

        // A selected purchase card fixes the currency of all prices sourced from it.
        Guid? selectedPriceCardId = request.PriceCardId ?? supplier?.DefaultPurchasePriceCardId;
        PriceCard? selectedPriceCard = null;
        if (selectedPriceCardId.HasValue)
        {
            if (_priceCardRepository == null)
                throw new InvalidOperationException("Purchase Price Card lookup is unavailable.");

            selectedPriceCard = await _priceCardRepository.GetPriceCardByIdAsync(
                tenantId, selectedPriceCardId.Value, includeLines: false);
            if (selectedPriceCard == null || !selectedPriceCard.IsActive ||
                selectedPriceCard.PriceCardType != PriceCardType.Purchase)
                throw new InvalidOperationException("The selected Purchase Price Card is missing, inactive, or has the wrong type.");
        }

        string? configuredSupplierCurrency = null;
        if (selectedPriceCard == null && supplier?.CurrencyId.HasValue == true && _currencyRepository != null)
        {
            var supplierCurrency = await _currencyRepository.GetCurrencyByIdAsync(tenantId, supplier.CurrencyId.Value);
            if (supplierCurrency?.IsActive == true)
                configuredSupplierCurrency = supplierCurrency.CurrencyCode;
        }

        string? requestedCurrencyCode = string.IsNullOrWhiteSpace(request.Currency)
            ? null
            : request.Currency.Trim().ToUpperInvariant();
        if (requestedCurrencyCode != null)
        {
            var requestedCurrency = await GetActiveCurrencyByCodeAsync(tenantId, requestedCurrencyCode);
            if (selectedPriceCard != null &&
                !string.Equals(requestedCurrency.CurrencyCode, selectedPriceCard.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Purchase Price Card currency '{selectedPriceCard.Currency}' must match the PO currency; it cannot be overridden.");
            requestedCurrencyCode = requestedCurrency.CurrencyCode;
        }

        order.PriceCardId = selectedPriceCard?.PriceCardId;
        string? priceSourceCurrency = null;

        // ── 2. Payment Terms ─────────────────────────────────────────────────────
        // Explicit request.PaymentTermId takes precedence over supplier default.
        Guid? resolvedPaymentTermId = request.PaymentTermId;
        if (resolvedPaymentTermId == null && supplier != null && _paymentTermRepository != null)
        {
            // Supplier stores PaymentTerms as a free-text string (no FK yet).
            // When the supplier string matches an active PaymentTerm.TermCode we resolve it.
            if (!string.IsNullOrWhiteSpace(supplier.PaymentTerms))
            {
                var allTerms = await _paymentTermRepository.GetByTenantAsync(tenantId);
                var matched = allTerms.FirstOrDefault(t =>
                    t.IsActive &&
                    string.Equals(t.TermCode, supplier.PaymentTerms.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                    resolvedPaymentTermId = matched.PaymentTermId;
            }
        }

        if (resolvedPaymentTermId.HasValue && _paymentTermRepository != null)
        {
            var term = await _paymentTermRepository.GetByIdAsync(tenantId, resolvedPaymentTermId.Value);
            if (term == null)
                throw new InvalidOperationException($"Payment term '{resolvedPaymentTermId.Value}' was not found or does not belong to this tenant.");
            if (!term.IsActive)
                throw new InvalidOperationException($"Payment term '{term.TermCode}' is inactive.");

            order.PaymentTermId = term.PaymentTermId;
            order.DueDate = order.OrderDate.AddDays(term.Days);
        }

        // ── 3. Lead time / Expected delivery date ────────────────────────────────
        // Explicit request value wins; otherwise we compute from price-card lead time later during item loop.
        if (request.ExpectedDeliveryDate.HasValue)
            order.ExpectedDeliveryDate = request.ExpectedDeliveryDate.Value;

        // Track earliest lead-time across items for header-level ExpectedDeliveryDate
        int? minLeadTimeDays = null;

        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemReq in request.Items)
            {
                var product = await _productRepository.GetByIdAsync(tenantId, itemReq.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException(
                        $"Product '{itemReq.ProductId}' was not found or does not belong to this tenant.");
                }

                // Resolve the active price card regardless of whether the unit price is overridden.
                Guid? uomId = itemReq.UnitOfMeasureId;
                Guid? supplierProductPriceId = null;
                SupplierProductPrice? activePrice = null;
                PriceResolutionResult? selectedCardPrice = null;
                if (request.SupplierId.HasValue)
                {
                    activePrice = await _supplierPriceService.GetActivePriceAsync(
                        tenantId,
                        request.SupplierId.Value,
                        itemReq.ProductId,
                        request.OrderDate ?? DateTime.UtcNow);
                }

                if (selectedPriceCard != null && !itemReq.UnitPrice.HasValue)
                {
                    if (_pricingResolutionService == null)
                        throw new InvalidOperationException("Purchase Price Card price lookup is unavailable.");

                    selectedCardPrice = await _pricingResolutionService.ResolvePurchasePriceAsync(
                        tenantId,
                        request.SupplierId,
                        itemReq.ProductId,
                        uomId,
                        request.OrderDate,
                        selectedPriceCard.PriceCardId);
                    if (!selectedCardPrice.Found)
                    {
                        throw new InvalidOperationException(
                            selectedCardPrice.Message ?? $"No price exists on Purchase Price Card '{selectedPriceCard.PriceCardCode}' for product '{product.ProductCode}'.");
                    }
                    if (!string.Equals(selectedCardPrice.Currency, selectedPriceCard.Currency, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Resolved Purchase Price Card price currency does not match the selected card currency.");
                }

                // A manual price may still be linked to a supplier price card.
                decimal unitPrice;
                if (itemReq.UnitPrice.HasValue)
                {
                    unitPrice = itemReq.UnitPrice.Value;
                }
                else if (selectedCardPrice?.Found == true)
                {
                    unitPrice = selectedCardPrice.UnitPrice;
                    uomId ??= selectedCardPrice.ResolvedUomId;
                }
                else if (activePrice != null)
                {
                    unitPrice = activePrice.UnitPrice;
                }
                else
                {
                    throw new ArgumentException(
                        request.SupplierId.HasValue
                            ? $"UnitPrice is required because no active price card exists for product '{product.ProductCode}' and supplier."
                            : "UnitPrice must be specified.");
                }

                if (activePrice != null)
                {
                    var usesLegacyPrice = selectedPriceCard == null ||
                        (selectedCardPrice?.Source == "SupplierProductPrice");
                    if (usesLegacyPrice)
                    {
                        var legacyCurrency = await GetActiveCurrencyByCodeAsync(
                            tenantId, activePrice.Currency);
                        if (selectedPriceCard != null &&
                            !string.Equals(legacyCurrency.CurrencyCode, selectedPriceCard.Currency, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Supplier price currency does not match the selected Purchase Price Card currency.");
                        if (requestedCurrencyCode != null &&
                            !string.Equals(legacyCurrency.CurrencyCode, requestedCurrencyCode, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("PO currency must match the selected supplier price currency.");
                        if (priceSourceCurrency != null &&
                            !string.Equals(priceSourceCurrency, legacyCurrency.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Supplier prices in different currencies cannot be combined on one purchase order.");

                        priceSourceCurrency = legacyCurrency.CurrencyCode;
                        supplierProductPriceId = activePrice.SupplierProductPriceId;
                        uomId ??= activePrice.UnitOfMeasureId ?? product.UnitOfMeasureId;
                    }

                    await ValidateMinimumOrderQuantityAsync(
                        tenantId,
                        companyId,
                        product,
                        itemReq.Quantity,
                        uomId,
                        activePrice);

                    if (activePrice.LeadTimeDays.HasValue &&
                        (minLeadTimeDays == null || activePrice.LeadTimeDays.Value > minLeadTimeDays.Value))
                    {
                        minLeadTimeDays = activePrice.LeadTimeDays.Value;
                    }
                }
                else
                {
                    uomId ??= product.UnitOfMeasureId;
                }

                if (uomId.HasValue)
                {
                    var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
                    if (uom == null)
                    {
                        throw new InvalidOperationException($"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
                    }
                }

                decimal taxRate = await ResolveTaxRateAsync(tenantId, itemReq.TaxCodeId, itemReq.TaxRate, order.OrderDate);
                order.AddItem(
                    itemReq.ProductId,
                    itemReq.Quantity,
                    unitPrice,
                    uomId,
                    itemReq.DiscountRate,
                    itemReq.TaxCodeId,
                    taxRate,
                    itemReq.IsTaxInclusive,
                    itemReq.Notes,
                    supplierProductPriceId);

            }
        }

        // ── 4. Expected Delivery Date (from lead time) ───────────────────────────
        // Only set if not already provided explicitly in the request.
        if (!order.ExpectedDeliveryDate.HasValue && minLeadTimeDays.HasValue)
        {
            order.ExpectedDeliveryDate = order.OrderDate.AddDays(minLeadTimeDays.Value);
        }

        string? nativeCurrencyCode = selectedPriceCard?.Currency
            ?? priceSourceCurrency
            ?? requestedCurrencyCode
            ?? configuredSupplierCurrency;
        if (nativeCurrencyCode == null)
            throw new InvalidOperationException(
                "Purchase order currency is required when no Price Card or supplier currency is configured.");

        if (_context != null)
        {
            await ApplyCurrencySnapshotAsync(order, nativeCurrencyCode);
        }
        else
        {
            order.Currency = nativeCurrencyCode;
        }

        try
        {
            await _repository.AddAsync(order);
            await _repository.SaveChangesAsync();
            await PopulateUnitOfMeasureCodesAsync(tenantId, new[] { order });
            return order;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_PurchaseOrders_TenantId_CompanyId_OrderNumber") ||
                msg.Contains("UNIQUE KEY") ||
                msg.Contains("duplicate key"))
            {
                throw new InvalidOperationException(
                    $"Purchase Order '{orderNumber}' already exists for this company.", ex);
            }

            throw;
        }
    }

    public async Task<PurchaseOrderItem> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        CreatePurchaseOrderItemRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var order = await _repository.GetByIdAsync(
            tenantId,
            companyId,
            branchId,
            orderId);

        if (order == null)
        {
            throw new KeyNotFoundException(
                "Purchase order was not found in the authorized branch/company scope.");
        }

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException(
                $"Product '{request.ProductId}' was not found or does not belong to this tenant.");
        }

        SupplierProductPrice? activePrice = null;
        if (order.SupplierId.HasValue)
        {
            activePrice = await _supplierPriceService.GetActivePriceAsync(
                tenantId,
                order.SupplierId.Value,
                request.ProductId,
                order.OrderDate);
        }

        PriceResolutionResult? selectedCardPrice = null;
        if (order.PriceCardId.HasValue && !request.UnitPrice.HasValue)
        {
            if (_pricingResolutionService == null)
                throw new InvalidOperationException("Purchase Price Card price lookup is unavailable.");
            selectedCardPrice = await _pricingResolutionService.ResolvePurchasePriceAsync(
                tenantId,
                order.SupplierId,
                request.ProductId,
                request.UnitOfMeasureId,
                order.OrderDate,
                order.PriceCardId);
            if (!selectedCardPrice.Found)
                throw new InvalidOperationException(selectedCardPrice.Message ?? "No applicable price exists on the selected Purchase Price Card.");
            if (!string.Equals(selectedCardPrice.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected Purchase Price Card price currency does not match the saved PO currency.");
        }

        Guid? uomId = request.UnitOfMeasureId;
        decimal unitPrice;
        if (request.UnitPrice.HasValue)
        {
            unitPrice = request.UnitPrice.Value;
        }
        else if (selectedCardPrice?.Found == true)
        {
            unitPrice = selectedCardPrice.UnitPrice;
            uomId ??= selectedCardPrice.ResolvedUomId;
        }
        else if (activePrice != null)
        {
            unitPrice = activePrice.UnitPrice;
        }
        else
        {
            throw new ArgumentException(
                order.SupplierId.HasValue
                    ? $"UnitPrice is required because no active price card exists for product '{product.ProductCode}' and supplier."
                    : "UnitPrice must be specified.");
        }

        Guid? supplierProductPriceId = null;
        if (activePrice != null)
        {
            var usesLegacyPrice = !order.PriceCardId.HasValue ||
                selectedCardPrice?.Source == "SupplierProductPrice";
            if (usesLegacyPrice)
            {
                var legacyCurrency = await GetActiveCurrencyByCodeAsync(tenantId, activePrice.Currency);
                if (!string.Equals(legacyCurrency.CurrencyCode, order.Currency, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Supplier price currency does not match the saved PO currency.");
                supplierProductPriceId = activePrice.SupplierProductPriceId;
                uomId ??= activePrice.UnitOfMeasureId ?? product.UnitOfMeasureId;
            }
            await ValidateMinimumOrderQuantityAsync(
                tenantId,
                companyId,
                product,
                request.Quantity,
                uomId,
                activePrice);
        }
        else
        {
            uomId ??= product.UnitOfMeasureId;
        }

        if (uomId.HasValue)
        {
            var uom = await _uomRepository.GetUnitByIdAsync(tenantId, uomId.Value);
            if (uom == null)
            {
                throw new InvalidOperationException($"Unit of measure '{uomId.Value}' was not found or does not belong to this tenant.");
            }
        }

        decimal taxRate = await ResolveTaxRateAsync(tenantId, request.TaxCodeId, request.TaxRate, order.OrderDate);
        order.AddItem(
            request.ProductId,
            request.Quantity,
            unitPrice,
            uomId,
            request.DiscountRate,
            request.TaxCodeId,
            taxRate,
            request.IsTaxInclusive,
            request.Notes,
            supplierProductPriceId);


        await _repository.SaveChangesAsync();

        await SetUnitOfMeasureCodeAsync(tenantId, order.Items[^1]);
        return order.Items[^1];
    }

    public async Task<PurchaseOrder> RefreshCurrencyAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        RefreshPurchaseOrderCurrencyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");
        if (order.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Exchange rates can only be refreshed on draft purchase orders.");

        var currency = request.TransactionCurrencyId.HasValue
            ? await GetActiveCurrencyAsync(tenantId, request.TransactionCurrencyId.Value)
            : order.TransactionCurrencyId.HasValue
                ? await GetActiveCurrencyAsync(tenantId, order.TransactionCurrencyId.Value)
                : await GetActiveCurrencyByCodeAsync(tenantId, order.Currency);

        if (order.PriceCardId.HasValue)
        {
            if (_priceCardRepository == null)
                throw new InvalidOperationException("Purchase Price Card lookup is unavailable.");
            var card = await _priceCardRepository.GetPriceCardByIdAsync(tenantId, order.PriceCardId.Value, includeLines: false);
            if (card == null || !card.IsActive || card.PriceCardType != PriceCardType.Purchase)
                throw new InvalidOperationException("The selected Purchase Price Card is missing or inactive.");
            if (!string.Equals(card.Currency, currency.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PO currency must match the selected Purchase Price Card currency.");
        }

        await ApplyCurrencySnapshotAsync(order, currency.CurrencyCode, request.OrderDate);
        await _repository.SaveChangesAsync();
        return order;
    }

    private async Task PopulateUnitOfMeasureCodesAsync(Guid tenantId, IEnumerable<PurchaseOrder> orders)
    {
        var items = orders.SelectMany(order => order.Items)
            .Where(item => item.UnitOfMeasureId.HasValue)
            .ToList();
        if (items.Count == 0)
            return;

        var units = await _uomRepository.GetUnitsAsync(tenantId);
        var unitsById = units.ToDictionary(unit => unit.UnitOfMeasureId);
        foreach (var item in items)
        {
            item.UnitOfMeasureCode = unitsById.TryGetValue(item.UnitOfMeasureId!.Value, out var unit)
                ? unit.Code
                : null;
        }
    }

    private async Task SetUnitOfMeasureCodeAsync(Guid tenantId, PurchaseOrderItem item)
    {
        if (!item.UnitOfMeasureId.HasValue)
            return;

        var unit = await _uomRepository.GetUnitByIdAsync(tenantId, item.UnitOfMeasureId.Value);
        item.UnitOfMeasureCode = unit?.Code;
    }

    private async Task ValidateMinimumOrderQuantityAsync(
        Guid tenantId,
        Guid companyId,
        Product product,
        decimal orderedQuantity,
        Guid? orderedUnitOfMeasureId,
        SupplierProductPrice price)
    {
        if (price.MinimumOrderQuantity <= 0)
            return;

        Guid? minimumQuantityUnitId = price.UnitOfMeasureId ?? product.UnitOfMeasureId;
        decimal comparableQuantity = Math.Round(orderedQuantity, 4, MidpointRounding.AwayFromZero);

        if (!orderedUnitOfMeasureId.HasValue || !minimumQuantityUnitId.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot validate the supplier minimum order quantity for product '{product.ProductCode}' because the purchase order and price card units of measure could not be established.");
        }

        if (orderedUnitOfMeasureId.Value != minimumQuantityUnitId.Value)
        {
            if (_uomService == null)
            {
                throw new InvalidOperationException(
                    "Unit of measure conversion is required to validate the supplier minimum order quantity.");
            }

            var converted = await _uomService.ConvertProductQuantityAsync(
                tenantId,
                product.ProductId,
                orderedUnitOfMeasureId.Value,
                minimumQuantityUnitId.Value,
                comparableQuantity,
                ConversionContext.Purchase,
                companyId: companyId);
            comparableQuantity = converted.ConvertedQuantity;
        }

        if (comparableQuantity < price.MinimumOrderQuantity)
        {
            throw new InvalidOperationException(
                $"Ordered quantity '{comparableQuantity}' for product '{product.ProductCode}' is less than the supplier price card minimum order quantity of '{price.MinimumOrderQuantity}'.");
        }
    }

    public async Task<PurchaseOrder> SubmitOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        await ValidateCurrencySnapshotAsync(order);
        var settings = await _approvalService.GetApprovalSettingsAsync(tenantId);

        if (settings.ApprovalRequired)
        {
            order.Submit();
            await _repository.SaveChangesAsync();

            await _approvalService.RecordSubmissionAsync(
                tenantId,
                "PurchaseOrder",
                order.Id,
                actorUserId,
                actorUserName,
                order.TotalAmount);
        }
        else
        {
            // Simple Approval disabled: automatically approve
            order.AutoApprove();
            await ApplyPurchaseOrderOnOrderDeltaAsync(order, isApproval: true);
            await _repository.SaveChangesAsync();

            await _approvalService.RecordAutoApprovalAsync(
                tenantId,
                "PurchaseOrder",
                order.Id,
                actorUserId,
                actorUserName,
                order.TotalAmount);
        }

        return order;
    }

    public async Task<PurchaseOrder> ApproveOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        await ValidateCurrencySnapshotAsync(order);
        // Guard self-approval
        Guid creatorId = order.CreatedByUserId ?? Guid.Empty;
        await _approvalService.RecordApprovalAsync(
            tenantId,
            "PurchaseOrder",
            order.Id,
            approverUserId,
            approverUserName,
            creatorId,
            order.TotalAmount,
            remarks);

        order.Approve();
        await ApplyPurchaseOrderOnOrderDeltaAsync(order, isApproval: true);
        await _repository.SaveChangesAsync();

        return order;
    }

    public async Task<PurchaseOrder> RejectOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid approverUserId,
        string approverUserName,
        string remarks)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        await _approvalService.RecordRejectionAsync(
            tenantId,
            "PurchaseOrder",
            order.Id,
            approverUserId,
            approverUserName,
            remarks,
            order.TotalAmount);

        order.Reject();
        await _repository.SaveChangesAsync();

        return order;
    }

    public async Task<PurchaseOrder> CancelOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        if (order.Status == PurchaseOrderStatus.Approved)
        {
            await ApplyPurchaseOrderOnOrderDeltaAsync(order, isApproval: false);
        }

        order.Cancel();
        await _repository.SaveChangesAsync();

        await _approvalService.RecordCancellationAsync(
            tenantId,
            "PurchaseOrder",
            order.Id,
            actorUserId,
            actorUserName,
            order.TotalAmount,
            remarks);

        return order;
    }

    public async Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        var order = await _repository.GetByIdAsync(tenantId, companyId, branchId, orderId)
            ?? throw new KeyNotFoundException("Purchase order not found in authorized scope.");

        return await _approvalService.GetApprovalHistoryAsync(
            tenantId,
            "PurchaseOrder",
            order.Id);
    }

    private async Task<Currency> GetActiveCurrencyByCodeAsync(Guid tenantId, string currencyCode)
    {
        if (_currencyRepository == null)
            throw new InvalidOperationException("Currency Master lookup is unavailable.");
        var currency = await _currencyRepository.GetCurrencyByCodeAsync(tenantId, currencyCode);
        if (currency == null || !currency.IsActive)
            throw new InvalidOperationException($"Currency '{currencyCode}' is missing or inactive for this tenant.");
        return currency;
    }

    private async Task<Currency> GetActiveCurrencyAsync(Guid tenantId, Guid currencyId)
    {
        if (_currencyRepository == null)
            throw new InvalidOperationException("Currency Master lookup is unavailable.");
        var currency = await _currencyRepository.GetCurrencyByIdAsync(tenantId, currencyId);
        if (currency == null || currency.TenantId != tenantId || !currency.IsActive)
            throw new InvalidOperationException("Currency is missing, inactive, or outside this tenant.");
        return currency;
    }

    private async Task<Currency> GetActiveCompanyBaseCurrencyAsync(Guid tenantId, Guid companyId)
    {
        if (_context == null)
            throw new InvalidOperationException("Company base currency lookup is unavailable.");
        var company = await _context.Companies
            .Include(candidate => candidate.BaseCurrency)
            .FirstOrDefaultAsync(candidate =>
                candidate.TenantId == tenantId &&
                candidate.Id == companyId &&
                candidate.IsActive);
        if (company?.BaseCurrency == null ||
            company.BaseCurrency.TenantId != tenantId ||
            !company.BaseCurrency.IsActive)
            throw new InvalidOperationException("The selected company has no active base currency.");
        return company.BaseCurrency;
    }

    private async Task ApplyCurrencySnapshotAsync(
        PurchaseOrder order,
        string transactionCurrencyCode,
        DateTime? transactionDate = null)
    {
        if (_exchangeRateService == null)
            throw new InvalidOperationException("Exchange rate lookup is unavailable.");

        var transactionCurrency = await GetActiveCurrencyByCodeAsync(order.TenantId, transactionCurrencyCode);
        var baseCurrency = await GetActiveCompanyBaseCurrencyAsync(order.TenantId, order.CompanyId);
        var effectiveDate = transactionDate ?? order.OrderDate;
        var rate = await _exchangeRateService.ResolveAsync(
            order.TenantId,
            transactionCurrency.CurrencyId,
            baseCurrency.CurrencyId,
            effectiveDate);

        if (transactionDate.HasValue)
            order.ChangeDraftOrderDate(transactionDate.Value);
        order.SetCurrencySnapshot(
            transactionCurrency.CurrencyId,
            transactionCurrency.CurrencyCode,
            baseCurrency.CurrencyId,
            baseCurrency.CurrencyCode,
            baseCurrency.DecimalPlaces,
            rate);
    }

    private async Task ValidateCurrencySnapshotAsync(PurchaseOrder order)
    {
        if (!order.TransactionCurrencyId.HasValue ||
            !order.BaseCurrencyId.HasValue ||
            !order.AppliedExchangeRate.HasValue ||
            !order.ExchangeRateSource.HasValue)
            throw new InvalidOperationException("Purchase order currency snapshot is incomplete; refresh the exchange rate while the order is a draft.");

        var transactionCurrency = await GetActiveCurrencyAsync(order.TenantId, order.TransactionCurrencyId.Value);
        if (!string.Equals(transactionCurrency.CurrencyCode, order.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Purchase order transaction currency no longer matches its saved currency snapshot.");

        var baseCurrency = await GetActiveCompanyBaseCurrencyAsync(order.TenantId, order.CompanyId);
        if (baseCurrency.CurrencyId != order.BaseCurrencyId.Value ||
            !string.Equals(baseCurrency.CurrencyCode, order.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Company base currency changed after this draft was created; refresh its currency snapshot before approval.");

        if (_exchangeRateService == null)
            throw new InvalidOperationException("Exchange rate lookup is unavailable.");
        var current = await _exchangeRateService.ResolveAsync(
            order.TenantId,
            transactionCurrency.CurrencyId,
            baseCurrency.CurrencyId,
            order.OrderDate);

        if (current.Rate != order.AppliedExchangeRate.Value ||
            current.Source != order.ExchangeRateSource.Value ||
            current.ExchangeRateId != order.ExchangeRateId ||
            current.EffectiveDate?.Date != order.ExchangeRateEffectiveDate?.Date)
            throw new InvalidOperationException("The applicable exchange rate changed after this draft was saved; explicitly refresh the draft rate before approval.");
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
            if (!taxCode.AppliesTo(TaxApplicability.Purchase))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' cannot be applied to purchase transactions.");
            }
            if (!taxCode.IsEffectiveAt(orderDate))
            {
                throw new InvalidOperationException($"Tax code '{taxCode.Code}' is not effective on {orderDate:yyyy-MM-dd}.");
            }
            return taxCode.Rate;
        }

        return explicitTaxRate;
    }

    private async Task ApplyPurchaseOrderOnOrderDeltaAsync(PurchaseOrder order, bool isApproval)
    {
        if (_context == null) return;

        var activeItems = order.Items.Where(i => i.IsActive && (i.Quantity - i.ReceivedQuantity) > 0).ToList();
        if (activeItems.Count == 0) return;

        var warehouses = await _context.Warehouses
            .Where(w => w.TenantId == order.TenantId && w.BranchId == order.BranchId && w.IsActive)
            .ToListAsync();

        var targetWarehouse = warehouses.FirstOrDefault();
        if (targetWarehouse == null)
        {
            targetWarehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.TenantId == order.TenantId && w.CompanyId == order.CompanyId && w.IsActive);
        }

        if (targetWarehouse == null) return;

        var locations = await _context.WarehouseLocations
            .Where(l => l.TenantId == order.TenantId && l.WarehouseId == targetWarehouse.WarehouseId && l.IsActive)
            .ToListAsync();

        var targetLocation = locations.FirstOrDefault();
        if (targetLocation == null) return;

        foreach (var item in activeItems)
        {
            var product = await _productRepository.GetByIdAsync(order.TenantId, item.ProductId);
            if (product == null || !product.IsStockTracked) continue;

            decimal delta = item.Quantity - item.ReceivedQuantity;
            if (delta <= 0) continue;

            decimal baseDelta = delta;
            if (item.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != product.UnitOfMeasureId.Value)
            {
                if (_uomService != null)
                {
                    var converted = await _uomService.ConvertProductQuantityAsync(
                        order.TenantId,
                        item.ProductId,
                        item.UnitOfMeasureId.Value,
                        product.UnitOfMeasureId.Value,
                        delta,
                        ConversionContext.Purchase,
                        companyId: order.CompanyId
                    );
                    baseDelta = converted.ConvertedQuantity;
                }
            }

            var balance = await _context.InventoryBalances
                .FirstOrDefaultAsync(b => b.TenantId == order.TenantId
                                       && b.WarehouseId == targetWarehouse.WarehouseId
                                       && b.WarehouseLocationId == targetLocation.WarehouseLocationId
                                       && b.ProductId == item.ProductId
                                       && b.BatchNumber == string.Empty
                                       && b.SerialNumber == string.Empty);

            if (balance == null && isApproval)
            {
                balance = ERP.Domain.Features.Inventory.InventoryBalance.Create(
                    order.TenantId,
                    targetWarehouse.WarehouseId,
                    targetLocation.WarehouseLocationId,
                    item.ProductId,
                    batchNumber: null,
                    serialNumber: null);
                _context.InventoryBalances.Add(balance);
            }

            if (balance != null)
            {
                decimal appliedDelta = isApproval ? baseDelta : -Math.Min(balance.QuantityOnOrder, baseDelta);
                if (appliedDelta != 0)
                {
                    balance.ApplyOnOrderChange(appliedDelta);
                }
            }
        }
    }
}