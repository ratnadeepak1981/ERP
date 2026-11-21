using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Ecommerce;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.Sales.SalesOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CompanyEntity = Domain.Features.MasterData.Company.Company;

namespace ERP.Application.Features.Ecommerce.Cart;

public class EcommerceCartService : IEcommerceCartService
{
    private readonly DomainDbContext _context;
    private readonly IPricingResolutionService _pricingResolutionService;
    private readonly ISalesOrderService _salesOrderService;
    private readonly ITaxCodeService? _taxCodeService;
    private readonly ILogger<EcommerceCartService> _logger;

    public EcommerceCartService(
        DomainDbContext context,
        IPricingResolutionService pricingResolutionService,
        ISalesOrderService salesOrderService,
        ILogger<EcommerceCartService> logger,
        ITaxCodeService? taxCodeService = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _pricingResolutionService = pricingResolutionService ?? throw new ArgumentNullException(nameof(pricingResolutionService));
        _salesOrderService = salesOrderService ?? throw new ArgumentNullException(nameof(salesOrderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _taxCodeService = taxCodeService;
    }

    public async Task<CartDto> GetCartAsync(
        Guid tenantId,
        Guid companyId,
        Guid? customerId,
        string? sessionId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var cart = await FindActiveCartAsync(tenantId, companyId, customerId, sessionId, ct);
        if (cart == null)
        {
            // Return empty cart structure for presentation
            var company = await GetCompanyAsync(tenantId, companyId, ct);
            return new CartDto
            {
                CartId = Guid.Empty,
                TenantId = tenantId,
                CompanyId = companyId,
                CompanyName = company.Name,
                CustomerId = customerId,
                SessionId = sessionId,
                Status = EcommerceCartStatus.Active,
                Currency = company.BaseCurrency?.CurrencyCode ?? "USD",
                TotalItems = 0,
                SubTotal = 0,
                TotalDiscount = 0,
                TotalTax = 0,
                GrandTotal = 0,
                Lines = new List<CartItemDto>()
            };
        }

        return await MapToDtoAsync(cart, ct);
    }

    public async Task<CartDto> AddToCartAsync(
        Guid tenantId,
        Guid companyId,
        AddToCartRequest request,
        Guid? customerId,
        Guid? customerContactId = null,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.ProductId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(request.ProductId));

        if (request.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be greater than zero.");

        // Validate Selling Company
        var company = await GetCompanyAsync(tenantId, companyId, ct);

        // Validate that product exists, is active, sellable, and published for this Selling Company
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == request.ProductId && p.IsActive, ct);

        if (product == null)
            throw new KeyNotFoundException($"Product '{request.ProductId}' was not found or is inactive.");

        if (!product.IsSellable)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not configured as sellable.");

        var publication = await _context.EcommerceProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.ProductId == request.ProductId, ct);

        if (publication == null || !publication.IsPublished)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not published for sale in this e-commerce catalogue.");

        // Validate UOM if provided
        Guid? uomId = request.UnitOfMeasureId ?? product.UnitOfMeasureId;
        if (uomId.HasValue)
        {
            var uomExists = await _context.UnitsOfMeasure
                .AnyAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == uomId.Value && u.IsActive, ct);
            if (!uomExists)
                throw new KeyNotFoundException($"Unit of Measure '{uomId.Value}' was not found or is inactive.");
        }

        // Validate ProductPackaging if provided
        if (request.ProductPackagingId.HasValue)
        {
            var packaging = await _context.ProductPackagings
                .AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.TenantId == tenantId && pp.ProductPackagingId == request.ProductPackagingId.Value && pp.ProductId == request.ProductId, ct);

            if (packaging == null || !packaging.IsActive)
                throw new KeyNotFoundException($"Packaging '{request.ProductPackagingId.Value}' was not found or is inactive.");

            if (!packaging.SalesAllowed)
                throw new InvalidOperationException($"Packaging '{packaging.PackagingCode}' is not permitted for sales.");
        }

        // Get or create active cart
        var cart = await GetOrCreateActiveCartAsync(tenantId, companyId, customerId, customerContactId, request.SessionId, ct);

        var line = cart.AddOrUpdateLine(request.ProductId, request.Quantity, uomId, request.ProductPackagingId);
        if (_context.Entry(line).State == EntityState.Detached)
        {
            _context.EcommerceCartLines.Add(line);
        }
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Added product {ProductId} (qty: {Quantity}) to cart {CartId} for company {CompanyId}",
            request.ProductId, request.Quantity, cart.Id, companyId);

        return await MapToDtoAsync(cart, ct);
    }

    public async Task<CartDto> UpdateLineQuantityAsync(
        Guid tenantId,
        Guid companyId,
        Guid lineId,
        UpdateCartLineRequest request,
        Guid? customerId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var cart = await FindActiveCartAsync(tenantId, companyId, customerId, request.SessionId, ct);
        if (cart == null)
            throw new KeyNotFoundException("No active shopping cart found.");

        var line = cart.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            throw new KeyNotFoundException($"Cart line '{lineId}' was not found.");

        if (request.Quantity <= 0)
        {
            cart.UpdateLineQuantity(lineId, 0);
            _context.EcommerceCartLines.Remove(line);
        }
        else
        {
            cart.UpdateLineQuantity(lineId, request.Quantity);
        }
        await _context.SaveChangesAsync(ct);

        return await MapToDtoAsync(cart, ct);
    }

    public async Task<CartDto> RemoveLineAsync(
        Guid tenantId,
        Guid companyId,
        Guid lineId,
        string? sessionId,
        Guid? customerId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var cart = await FindActiveCartAsync(tenantId, companyId, customerId, sessionId, ct);
        if (cart == null)
            throw new KeyNotFoundException("No active shopping cart found.");

        var line = cart.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line != null)
        {
            cart.RemoveLine(lineId);
            _context.EcommerceCartLines.Remove(line);
            await _context.SaveChangesAsync(ct);
        }

        return await MapToDtoAsync(cart, ct);
    }

    public async Task<CartDto> ClearCartAsync(
        Guid tenantId,
        Guid companyId,
        string? sessionId,
        Guid? customerId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var cart = await FindActiveCartAsync(tenantId, companyId, customerId, sessionId, ct);
        if (cart != null)
        {
            foreach (var line in cart.Lines.ToList())
            {
                _context.EcommerceCartLines.Remove(line);
            }
            cart.ClearLines();
            await _context.SaveChangesAsync(ct);
            return await MapToDtoAsync(cart, ct);
        }

        return await GetCartAsync(tenantId, companyId, customerId, sessionId, ct);
    }

    public async Task<CheckoutResultDto> CheckoutAsync(
        Guid tenantId,
        Guid companyId,
        CheckoutRequest request,
        Guid? customerId,
        Guid? customerContactId = null,
        Guid? actorUserId = null,
        string? actorUserName = null,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // 1. Idempotency Check: prevent duplicate submission
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            string key = request.IdempotencyKey.Trim();
            var existingCheckedOutCart = await _context.EcommerceCarts
                .AsNoTracking()
                .Include(c => c.SalesOrder)
                .FirstOrDefaultAsync(c =>
                    c.TenantId == tenantId &&
                    c.CompanyId == companyId &&
                    c.IdempotencyKey == key &&
                    c.Status == EcommerceCartStatus.CheckedOut,
                    ct);

            if (existingCheckedOutCart != null && existingCheckedOutCart.SalesOrderId.HasValue)
            {
                var salesOrder = existingCheckedOutCart.SalesOrder ??
                    await _context.SalesOrders.AsNoTracking().FirstOrDefaultAsync(so => so.Id == existingCheckedOutCart.SalesOrderId.Value, ct);

                _logger.LogInformation("Idempotency hit for key '{Key}'. Returning existing sales order {OrderId}",
                    key, existingCheckedOutCart.SalesOrderId);

                return new CheckoutResultDto
                {
                    Success = true,
                    SalesOrderId = existingCheckedOutCart.SalesOrderId.Value,
                    OrderNumber = salesOrder?.OrderNumber ?? $"SO-WEB-{existingCheckedOutCart.SalesOrderId.Value.ToString()[..6]}",
                    OrderStatus = salesOrder?.Status ?? SalesOrderStatus.Draft,
                    CartId = existingCheckedOutCart.Id,
                    TotalAmount = salesOrder?.TotalAmount ?? 0m,
                    Currency = salesOrder?.Currency ?? "USD",
                    Message = "Order was already processed successfully (idempotent submission)."
                };
            }
        }

        // 2. Find Active Cart
        Guid? effectiveCustomerId = customerId ?? request.CustomerId;
        var cart = await FindActiveCartAsync(tenantId, companyId, effectiveCustomerId, request.SessionId, ct);
        if (cart == null || cart.Lines.Count == 0)
        {
            throw new InvalidOperationException("Cannot checkout an empty shopping cart.");
        }

        // Record checkout attempt timestamp
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            cart.RecordCheckoutAttempt(request.IdempotencyKey);
        }

        // 3. Resolve Selling Company and Currency
        var company = await GetCompanyAsync(tenantId, companyId, ct);
        string orderCurrency = company.BaseCurrency?.CurrencyCode ?? "USD";

        // 4. Resolve Active Branch for Selling Company
        var branches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .ToListAsync(ct);

        if (branches.Count == 0)
        {
            throw new InvalidOperationException($"No active branch is configured for Selling Company '{company.Name}'.");
        }

        var branch = branches.FirstOrDefault(b => b.Code.Equals("MAIN", StringComparison.OrdinalIgnoreCase) ||
                                                  b.Code.Equals("HQ", StringComparison.OrdinalIgnoreCase))
                     ?? branches.First();

        // 5. Resolve Customer Context
        Customer? customer = null;
        if (effectiveCustomerId.HasValue)
        {
            customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerId == effectiveCustomerId.Value && c.IsActive, ct);
        }

        if (customer == null)
        {
            // Anonymous / Walk-in checkout: fallback to tenant walk-in customer CUST-WALKIN
            customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CustomerCode == "CUST-WALKIN" && c.IsActive, ct);

            if (customer == null)
            {
                // Fallback to any active customer in tenant if CUST-WALKIN is missing
                customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive, ct);
            }

            if (customer == null)
            {
                throw new InvalidOperationException("No active customer could be resolved for checkout.");
            }
        }

        // 6. Validate published status & resolve server-side pricing and tax for all cart lines
        var orderItems = new List<CreateSalesOrderItemRequest>();
        var activeTaxCode = await GetActiveSalesTaxCodeAsync(tenantId, ct);

        foreach (var line in cart.Lines)
        {
            // Verify product is still active, sellable, and published
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == line.ProductId && p.IsActive, ct);

            if (product == null || !product.IsSellable)
            {
                throw new InvalidOperationException($"Product '{line.ProductId}' in your cart is no longer available.");
            }

            var isPublished = await _context.EcommerceProducts
                .AnyAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.ProductId == line.ProductId && ep.IsPublished, ct);

            if (!isPublished)
            {
                throw new InvalidOperationException($"Product '{product.ProductCode}' is no longer published for sale in this catalogue.");
            }

            // Real-time server-side price resolution
            var priceResult = await _pricingResolutionService.ResolveSalesPriceAsync(
                tenantId,
                customer.CustomerId,
                line.ProductId,
                line.UnitOfMeasureId,
                DateTime.UtcNow);

            decimal unitPrice = priceResult.Found ? priceResult.UnitPrice : product.SellingPrice;
            if (unitPrice <= 0)
            {
                throw new InvalidOperationException($"No selling price is configured for product '{product.ProductCode}'.");
            }

            decimal taxRate = activeTaxCode?.Rate ?? 0m;
            Guid? taxCodeId = activeTaxCode?.TaxCodeId;

            orderItems.Add(new CreateSalesOrderItemRequest
            {
                ProductId = line.ProductId,
                UnitOfMeasureId = line.UnitOfMeasureId ?? product.UnitOfMeasureId,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                DiscountRate = 0m,
                TaxCodeId = taxCodeId,
                TaxRate = taxRate,
                IsTaxInclusive = false,
                Notes = line.ProductPackagingId.HasValue ? $"Packaging: {line.ProductPackagingId.Value}" : null
            });
        }

        // 7. Validate delivery address if provided
        Guid? deliveryAddressId = request.DeliveryAddressId;
        if (deliveryAddressId.HasValue)
        {
            var addressValid = await _context.CustomerAddresses
                .AnyAsync(ca => ca.TenantId == tenantId && ca.CustomerId == customer.CustomerId && ca.AddressId == deliveryAddressId.Value && ca.IsActive, ct);

            if (!addressValid)
            {
                // Fallback check on Addresses table
                addressValid = await _context.Addresses
                    .AnyAsync(a => a.TenantId == tenantId && a.AddressId == deliveryAddressId.Value && a.IsActive, ct);

                if (!addressValid)
                {
                    throw new InvalidOperationException($"Delivery address '{deliveryAddressId.Value}' was not found or is inactive.");
                }
            }
        }

        // 8. Generate Order Number & Create Sales Order in Draft Status
        string orderNumber = $"SO-WEB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var createOrderRequest = new CreateSalesOrderRequest
        {
            OrderNumber = orderNumber,
            DocumentType = SalesOrderType.Standard,
            CustomerId = customer.CustomerId,
            OrderDate = DateTime.UtcNow,
            DeliveryDate = request.RequestedDeliveryDate,
            Currency = orderCurrency,
            DeliveryAddressId = deliveryAddressId,
            Notes = $"E-commerce Online Order | Session: {cart.SessionId ?? "Customer"} | Notes: {request.CustomerNotes}".Trim(),
            Items = orderItems
        };

        var salesOrderDto = await _salesOrderService.CreateOrderAsync(
            tenantId,
            companyId,
            branch.Id,
            createOrderRequest,
            actorUserId,
            actorUserName ?? "EcommerceCheckout");

        // 9. Update Cart status to CheckedOut
        cart.MarkCheckedOut(salesOrderDto.Id, request.IdempotencyKey);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Successfully completed free e-commerce checkout. Cart: {CartId}, SalesOrder: {OrderId}, Number: {OrderNumber}",
            cart.Id, salesOrderDto.Id, salesOrderDto.OrderNumber);

        return new CheckoutResultDto
        {
            Success = true,
            SalesOrderId = salesOrderDto.Id,
            OrderNumber = salesOrderDto.OrderNumber,
            OrderStatus = salesOrderDto.Status,
            CartId = cart.Id,
            TotalAmount = salesOrderDto.TotalAmount,
            Currency = salesOrderDto.Currency,
            Message = "Sales order created successfully via free e-commerce checkout."
        };
    }

    private async Task<EcommerceCart?> FindActiveCartAsync(
        Guid tenantId,
        Guid companyId,
        Guid? customerId,
        string? sessionId,
        CancellationToken ct)
    {
        IQueryable<EcommerceCart> query = _context.EcommerceCarts
            .Include(c => c.Lines)
            .Where(c => c.TenantId == tenantId && c.CompanyId == companyId && c.Status == EcommerceCartStatus.Active);

        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            var cart = await query.FirstOrDefaultAsync(c => c.CustomerId == customerId.Value, ct);
            if (cart != null) return cart;
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            string session = sessionId.Trim();
            var cart = await query.FirstOrDefaultAsync(c => c.SessionId == session, ct);
            if (cart != null)
            {
                // If user is now authenticated, associate customer with this guest cart
                if (customerId.HasValue && customerId.Value != Guid.Empty && !cart.CustomerId.HasValue)
                {
                    cart.AssociateCustomer(customerId.Value);
                    await _context.SaveChangesAsync(ct);
                }
                return cart;
            }
        }

        return null;
    }

    private async Task<EcommerceCart> GetOrCreateActiveCartAsync(
        Guid tenantId,
        Guid companyId,
        Guid? customerId,
        Guid? customerContactId,
        string? sessionId,
        CancellationToken ct)
    {
        var existing = await FindActiveCartAsync(tenantId, companyId, customerId, sessionId, ct);
        if (existing != null)
        {
            return existing;
        }

        string effectiveSessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("N") : sessionId.Trim();
        var newCart = EcommerceCart.Create(tenantId, companyId, customerId, customerContactId, effectiveSessionId);

        _context.EcommerceCarts.Add(newCart);
        await _context.SaveChangesAsync(ct);

        return newCart;
    }

    private async Task<CartDto> MapToDtoAsync(EcommerceCart cart, CancellationToken ct)
    {
        var company = await GetCompanyAsync(cart.TenantId, cart.CompanyId, ct);
        string currency = company.BaseCurrency?.CurrencyCode ?? "USD";

        string? customerName = null;
        if (cart.CustomerId.HasValue)
        {
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == cart.TenantId && c.CustomerId == cart.CustomerId.Value, ct);
            customerName = customer?.CustomerName;
        }

        var linesDto = new List<CartItemDto>();
        decimal subTotal = 0m;
        decimal totalTax = 0m;
        var activeTaxCode = await GetActiveSalesTaxCodeAsync(cart.TenantId, ct);

        foreach (var line in cart.Lines)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == cart.TenantId && p.ProductId == line.ProductId, ct);

            string productCode = product?.ProductCode ?? string.Empty;
            string productName = product?.ProductName ?? "Unknown Product";
            bool isAvailable = product != null && product.IsActive && product.IsSellable;

            var publication = await _context.EcommerceProducts
                .AsNoTracking()
                .FirstOrDefaultAsync(ep => ep.TenantId == cart.TenantId && ep.CompanyId == cart.CompanyId && ep.ProductId == line.ProductId, ct);

            if (publication == null || !publication.IsPublished)
            {
                isAvailable = false;
            }

            // Real-time server-side price resolution
            decimal unitPrice = 0m;
            string? validationMsg = null;

            if (product != null)
            {
                var priceResult = await _pricingResolutionService.ResolveSalesPriceAsync(
                    cart.TenantId,
                    cart.CustomerId ?? Guid.Empty,
                    line.ProductId,
                    line.UnitOfMeasureId,
                    DateTime.UtcNow);

                unitPrice = priceResult.Found ? priceResult.UnitPrice : product.SellingPrice;
            }

            if (!isAvailable)
            {
                validationMsg = "Item is currently not available for purchase.";
            }

            decimal lineSubTotal = Math.Round(line.Quantity * unitPrice, 2);
            decimal taxRate = activeTaxCode?.Rate ?? 0m;
            decimal lineTax = Math.Round(lineSubTotal * (taxRate / 100m), 2);
            decimal lineTotal = lineSubTotal + lineTax;

            subTotal += lineSubTotal;
            totalTax += lineTax;

            // UOM details
            string? uomCode = product?.UnitOfMeasure;
            string? uomName = null;
            if (line.UnitOfMeasureId.HasValue)
            {
                var uom = await _context.UnitsOfMeasure
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.TenantId == cart.TenantId && u.UnitOfMeasureId == line.UnitOfMeasureId.Value, ct);
                if (uom != null)
                {
                    uomCode = uom.Code;
                    uomName = uom.Name;
                }
            }

            // Packaging details
            string? packagingName = null;
            if (line.ProductPackagingId.HasValue)
            {
                var packaging = await _context.ProductPackagings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pp => pp.ProductPackagingId == line.ProductPackagingId.Value, ct);
                packagingName = packaging?.PackagingName;
            }

            linesDto.Add(new CartItemDto
            {
                LineId = line.Id,
                ProductId = line.ProductId,
                ProductCode = productCode,
                ProductName = productName,
                UnitOfMeasureId = line.UnitOfMeasureId,
                UnitOfMeasureCode = uomCode,
                UnitOfMeasureName = uomName,
                ProductPackagingId = line.ProductPackagingId,
                PackagingName = packagingName,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                SubTotal = lineSubTotal,
                DiscountRate = 0m,
                DiscountAmount = 0m,
                TaxCodeId = activeTaxCode?.TaxCodeId,
                TaxCode = activeTaxCode?.Code,
                TaxRate = taxRate,
                TaxAmount = lineTax,
                LineTotal = lineTotal,
                IsTaxInclusive = false,
                Currency = currency,
                IsAvailable = isAvailable,
                ValidationMessage = validationMsg
            });
        }

        return new CartDto
        {
            CartId = cart.Id,
            TenantId = cart.TenantId,
            CompanyId = cart.CompanyId,
            CompanyName = company.Name,
            CustomerId = cart.CustomerId,
            CustomerName = customerName,
            SessionId = cart.SessionId,
            Status = cart.Status,
            Currency = currency,
            TotalItems = cart.Lines.Count,
            SubTotal = subTotal,
            TotalDiscount = 0m,
            TotalTax = totalTax,
            GrandTotal = subTotal + totalTax,
            Lines = linesDto
        };
    }

    private async Task<CompanyEntity> GetCompanyAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        var company = await _context.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId && c.IsActive, ct);

        if (company == null)
            throw new KeyNotFoundException($"Selling company '{companyId}' was not found or is inactive.");

        return company;
    }

    private async Task<TaxCode?> GetActiveSalesTaxCodeAsync(Guid tenantId, CancellationToken ct)
    {
        return await _context.TaxCodes
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && t.Applicability == TaxApplicability.Sales)
            .OrderByDescending(t => t.Rate)
            .FirstOrDefaultAsync(ct);
    }

    private static void ValidateTenantAndCompany(Guid tenantId, Guid companyId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
    }
}
