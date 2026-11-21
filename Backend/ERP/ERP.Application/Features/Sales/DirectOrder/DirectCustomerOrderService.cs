using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Sales.SalesOrder;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.DirectOrder;

public class DirectCustomerOrderService : IDirectCustomerOrderService
{
    private readonly DomainDbContext _dbContext;
    private readonly ISalesOrderService _salesOrderService;
    private readonly IUnitOfMeasureService? _uomService;

    public DirectCustomerOrderService(
        DomainDbContext dbContext,
        ISalesOrderService salesOrderService,
        IUnitOfMeasureService? uomService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _salesOrderService = salesOrderService ?? throw new ArgumentNullException(nameof(salesOrderService));
        _uomService = uomService;
    }

    public async Task<SalesOrderDto> CreateDirectB2BOrderAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        CreateDirectB2BOrderRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID is required.", nameof(customerId));

        if (request.SellingCompanyId == Guid.Empty)
            throw new ArgumentException("Selling company ID is required.", nameof(request.SellingCompanyId));

        if (request.Items == null || request.Items.Count == 0)
            throw new ArgumentException("At least one order line item is required.", nameof(request.Items));

        // 1. Validate Customer Master & Contact Membership
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive, ct)
            ?? throw new InvalidOperationException("Customer account is inactive or not found.");

        var contactMembership = await _dbContext.CustomerContacts
            .AsNoTracking()
            .FirstOrDefaultAsync(cc => cc.TenantId == tenantId && cc.CustomerId == customerId && cc.ContactId == customerContactId && cc.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Customer contact is not an authorized active member of this customer company.");

        // 2. Validate Selling Company & Active Scope
        var sellingCompany = await _dbContext.Companies
            .Include(c => c.BaseCurrency)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.SellingCompanyId && c.IsActive, ct)
            ?? throw new KeyNotFoundException("Selling Company was not found or is inactive.");

        // Resolve active Branch
        Guid branchId;
        if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
        {
            var branch = await _dbContext.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.CompanyId == request.SellingCompanyId && b.Id == request.BranchId.Value && b.IsActive, ct)
                ?? throw new KeyNotFoundException("Branch not found or inactive for the selected Selling Company.");
            branchId = branch.Id;
        }
        else
        {
            var defaultBranch = await _dbContext.Branches
                .AsNoTracking()
                .Where(b => b.CompanyId == request.SellingCompanyId && b.IsActive)
                .OrderBy(b => b.Code)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"No active branch configured for selling company '{sellingCompany.Name}'.");
            branchId = defaultBranch.Id;
        }

        // 3. Resolve Customer Assigned Price Card
        if (!customer.DefaultSalesPriceCardId.HasValue)
        {
            throw new InvalidOperationException("Your corporate account does not currently have an assigned contract Sales Price Card. Please explore our catalog or request a formal Sales Quotation.");
        }

        var priceCard = await _dbContext.PriceCards
            .Include(pc => pc.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(pc => pc.TenantId == tenantId && pc.PriceCardId == customer.DefaultSalesPriceCardId.Value && pc.IsActive, ct)
            ?? throw new InvalidOperationException("The customer's assigned contract Sales Price Card was not found or is inactive.");

        if (priceCard.PriceCardType != PriceCardType.Sales)
        {
            throw new InvalidOperationException($"Assigned Price Card '{priceCard.PriceCardCode}' is not a Sales Price Card.");
        }

        var now = DateTime.UtcNow;
        if (priceCard.EffectiveFrom > now || (priceCard.EffectiveTo.HasValue && priceCard.EffectiveTo.Value < now))
        {
            throw new InvalidOperationException($"Assigned Sales Price Card '{priceCard.PriceCardCode}' is not active on the current date.");
        }

        // 4. Idempotency Check
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingOrder = await _dbContext.SalesOrders
                .Include(so => so.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.TenantId == tenantId &&
                                           so.CustomerId == customerId &&
                                           so.CompanyId == request.SellingCompanyId &&
                                           so.Notes != null && so.Notes.Contains($"[IdempotencyKey:{request.IdempotencyKey.Trim()}]"), ct);

            if (existingOrder != null)
            {
                return MapToDto(existingOrder);
            }
        }

        // 5. Validate Delivery Address Ownership if Provided
        string? addressSnapshot = request.DeliveryAddress;
        if (request.DeliveryAddressId.HasValue && request.DeliveryAddressId.Value != Guid.Empty)
        {
            var address = await _dbContext.Addresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AddressId == request.DeliveryAddressId.Value && a.IsActive, ct);

            if (address != null)
            {
                addressSnapshot = $"{address.AddressLine1}, {address.City}".Trim(',', ' ');
            }
        }

        // 6. Build Order Lines Enforcing Price Card Rates & Minimum Order Quantities
        var orderItems = new List<CreateSalesOrderItemRequest>();
        var activeTaxCodes = await _dbContext.TaxCodes
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && t.Applicability == TaxApplicability.Sales)
            .ToListAsync(ct);

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException("Item quantity must be strictly greater than zero.");

            var product = await _dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == item.ProductId && p.IsActive, ct)
                ?? throw new KeyNotFoundException($"Product '{item.ProductId}' was not found or is inactive.");

            if (!product.IsSellable)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is marked not sellable.");

            // Resolve line on contract Price Card
            var cardLine = priceCard.Lines.FirstOrDefault(l =>
                l.ProductId == item.ProductId &&
                l.IsActive &&
                (!l.EffectiveFrom.HasValue || l.EffectiveFrom.Value <= now) &&
                (!l.EffectiveTo.HasValue || l.EffectiveTo.Value >= now));

            if (cardLine == null)
            {
                throw new InvalidOperationException($"Product '{product.ProductCode}' does not exist on your assigned contract Sales Price Card '{priceCard.PriceCardCode}'.");
            }

            // Enforce Minimum Order Quantity (MOQ)
            if (item.Quantity < cardLine.MinimumQuantity)
            {
                throw new InvalidOperationException($"Requested quantity {item.Quantity} for product '{product.ProductCode}' does not meet the contract minimum order quantity (MOQ) of {cardLine.MinimumQuantity}.");
            }

            decimal unitPrice = cardLine.UnitPrice;
            Guid? uomId = item.UnitOfMeasureId ?? cardLine.UnitOfMeasureId ?? product.UnitOfMeasureId;

            // Apply UOM Conversion if requested UOM differs from contract line UOM
            if (item.UnitOfMeasureId.HasValue && cardLine.UnitOfMeasureId.HasValue && item.UnitOfMeasureId.Value != cardLine.UnitOfMeasureId.Value && _uomService != null)
            {
                try
                {
                    var conv = await _uomService.ConvertProductQuantityAsync(
                        tenantId,
                        product.ProductId,
                        item.UnitOfMeasureId.Value,
                        cardLine.UnitOfMeasureId.Value,
                        1m,
                        ConversionContext.Sales);

                    if (conv != null && conv.ConvertedQuantity > 0)
                    {
                        unitPrice = Math.Round(cardLine.UnitPrice * conv.ConvertedQuantity, 4);
                    }
                }
                catch { }
            }

            // Tax resolution
            decimal taxRate = 0m;
            Guid? taxCodeId = null;
            var defaultTax = activeTaxCodes.OrderByDescending(t => t.Rate).FirstOrDefault();
            if (defaultTax != null)
            {
                taxCodeId = defaultTax.TaxCodeId;
                taxRate = defaultTax.Rate;
            }

            orderItems.Add(new CreateSalesOrderItemRequest
            {
                ProductId = product.ProductId,
                UnitOfMeasureId = uomId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                DiscountRate = 0m,
                TaxCodeId = taxCodeId,
                TaxRate = taxRate,
                IsTaxInclusive = false,
                Notes = item.Notes
            });
        }

        // 7. Formulate Order Creation Request
        string noteText = $"Direct B2B Order via Price Card '{priceCard.PriceCardCode}'";
        if (!string.IsNullOrWhiteSpace(request.CustomerRemarks))
        {
            noteText += $" | {request.CustomerRemarks.Trim()}";
        }
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            noteText += $" [IdempotencyKey:{request.IdempotencyKey.Trim()}]";
        }

        string orderNumber = $"SO-B2B-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var createOrderRequest = new CreateSalesOrderRequest
        {
            OrderNumber = orderNumber,
            DocumentType = SalesOrderType.Standard,
            CustomerId = customerId,
            OrderDate = now,
            Currency = priceCard.Currency,
            PaymentTerms = customer.PaymentTerms ?? "Net 30 Days",
            Notes = noteText,
            DeliveryAddressId = request.DeliveryAddressId,
            Items = orderItems
        };

        // 8. Invoke Authoritative SalesOrderService to generate Draft order
        var orderDto = await _salesOrderService.CreateOrderAsync(
            tenantId,
            request.SellingCompanyId,
            branchId,
            createOrderRequest,
            actorUserId: customerContactId,
            actorUserName: customer.ContactPerson ?? "B2B Customer");

        return orderDto;
    }

    private static SalesOrderDto MapToDto(global::Domain.Features.Sales.SalesOrder.SalesOrder order)
    {
        return new SalesOrderDto
        {
            Id = order.Id,
            TenantId = order.TenantId,
            CompanyId = order.CompanyId,
            BranchId = order.BranchId,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            DocumentType = order.DocumentType,
            OrderDate = order.OrderDate,
            DeliveryDate = order.DeliveryDate,
            Currency = order.Currency,
            PaymentTerms = order.PaymentTerms,
            Status = order.Status,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            Notes = order.Notes,
            DeliveryAddressId = order.DeliveryAddressId,
            DeliveryAddressSnapshot = order.DeliveryAddressSnapshot,
            Items = order.Items.Select(i => new SalesOrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                UnitOfMeasureId = i.UnitOfMeasureId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountRate = i.DiscountRate,
                DiscountAmount = i.DiscountAmount,
                TaxCodeId = i.TaxCodeId,
                TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount,
                SubTotal = i.SubTotal,
                LineTotal = i.LineTotal,
                IsTaxInclusive = i.IsTaxInclusive,
                Notes = i.Notes
            }).ToList()
        };
    }
}
