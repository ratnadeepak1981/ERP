using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;

namespace Domain.Features.MasterData.PriceCard;

public class PricingResolutionService : IPricingResolutionService
{
    private readonly IPriceCardService _priceCardService;
    private readonly IPriceCardRepository _priceCardRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISupplierProductPriceService _supplierProductPriceService;
    private readonly IUnitOfMeasureService? _uomService;

    public PricingResolutionService(
        IPriceCardService priceCardService,
        IPriceCardRepository priceCardRepository,
        ISupplierRepository supplierRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ISupplierProductPriceService supplierProductPriceService,
        IUnitOfMeasureService? uomService = null)
    {
        _priceCardService = priceCardService;
        _priceCardRepository = priceCardRepository;
        _supplierRepository = supplierRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _supplierProductPriceService = supplierProductPriceService;
        _uomService = uomService;
    }

    public async Task<PriceResolutionResult> ResolvePurchasePriceAsync(
        Guid tenantId,
        Guid? supplierId,
        Guid productId,
        Guid? requestedUomId,
        DateTime? transactionDate = null,
        Guid? overridePriceCardId = null)
    {
        DateTime checkDate = transactionDate ?? DateTime.UtcNow;

        // 1. Transaction-level override
        if (overridePriceCardId.HasValue)
        {
            var card = await _priceCardRepository.GetPriceCardByIdAsync(tenantId, overridePriceCardId.Value, includeLines: false);
            if (card == null)
            {
                return new PriceResolutionResult
                {
                    Found = false,
                    ProductId = productId,
                    Message = "Selected Purchase Price Card was not found."
                };
            }

            if (card.PriceCardType != PriceCardType.Purchase)
            {
                return new PriceResolutionResult
                {
                    Found = false,
                    ProductId = productId,
                    Message = $"Price Card '{card.PriceCardCode}' is of type {card.PriceCardType} and cannot be used in a Purchase transaction."
                };
            }

            return await _priceCardService.GetApplicablePriceAsync(tenantId, card.PriceCardId, productId, requestedUomId, checkDate);
        }

        // 2. Supplier default purchase price card
        if (supplierId.HasValue)
        {
            var supplier = await _supplierRepository.GetSupplierByIdAsync(tenantId, supplierId.Value);
            if (supplier != null && supplier.DefaultPurchasePriceCardId.HasValue)
            {
                var card = await _priceCardRepository.GetPriceCardByIdAsync(tenantId, supplier.DefaultPurchasePriceCardId.Value, includeLines: false);
                if (card != null && card.PriceCardType == PriceCardType.Purchase && card.IsActive)
                {
                    var result = await _priceCardService.GetApplicablePriceAsync(tenantId, card.PriceCardId, productId, requestedUomId, checkDate);
                    if (result.Found)
                    {
                        return result;
                    }
                }
            }

            // 3. Backward compatibility: Fallback to existing SupplierProductPrice
            var legacyPrice = await _supplierProductPriceService.GetActivePriceAsync(tenantId, supplierId.Value, productId, checkDate);
            if (legacyPrice != null)
            {
                decimal unitPrice = legacyPrice.UnitPrice;
                Guid? resolvedUom = legacyPrice.UnitOfMeasureId;

                if (requestedUomId.HasValue && legacyPrice.UnitOfMeasureId.HasValue && requestedUomId.Value != legacyPrice.UnitOfMeasureId.Value && _uomService != null)
                {
                    try
                    {
                        var conv = await _uomService.ConvertProductQuantityAsync(
                            tenantId,
                            productId,
                            requestedUomId.Value,
                            legacyPrice.UnitOfMeasureId.Value,
                            1m,
                            ConversionContext.Purchase);

                        if (conv != null && conv.ConvertedQuantity > 0)
                        {
                            unitPrice = Math.Round(legacyPrice.UnitPrice * conv.ConvertedQuantity, 4);
                            resolvedUom = requestedUomId.Value;
                        }
                    }
                    catch { }
                }

                return new PriceResolutionResult
                {
                    Found = true,
                    ProductId = productId,
                    ResolvedUomId = resolvedUom,
                    UnitPrice = unitPrice,
                    Currency = legacyPrice.Currency,
                    ExchangeRate = 1m,
                    Source = "SupplierProductPrice"
                };
            }
        }

        return new PriceResolutionResult
        {
            Found = false,
            ProductId = productId,
            Message = "No applicable purchase price found on any assigned price card or supplier price configuration."
        };
    }

    public async Task<PriceResolutionResult> ResolveSalesPriceAsync(
        Guid tenantId,
        Guid customerId,
        Guid productId,
        Guid? requestedUomId,
        DateTime? transactionDate = null,
        Guid? overridePriceCardId = null)
    {
        DateTime checkDate = transactionDate ?? DateTime.UtcNow;

        var customer = await _customerRepository.GetCustomerByIdAsync(tenantId, customerId);
        if (customer == null)
        {
            return new PriceResolutionResult
            {
                Found = false,
                ProductId = productId,
                Message = "Customer not found."
            };
        }

        Guid? activeCardId = overridePriceCardId ?? customer.DefaultSalesPriceCardId;

        // If a Sales Price Card is configured or overridden
        if (activeCardId.HasValue)
        {
            var card = await _priceCardRepository.GetPriceCardByIdAsync(tenantId, activeCardId.Value, includeLines: false);
            if (card == null)
            {
                return new PriceResolutionResult
                {
                    Found = false,
                    ProductId = productId,
                    Message = "Configured Sales Price Card was not found."
                };
            }

            if (card.PriceCardType != PriceCardType.Sales)
            {
                return new PriceResolutionResult
                {
                    Found = false,
                    ProductId = productId,
                    Message = $"Price Card '{card.PriceCardCode}' is of type {card.PriceCardType} and cannot be used in a Sales transaction."
                };
            }

            var cardResult = await _priceCardService.GetApplicablePriceAsync(tenantId, card.PriceCardId, productId, requestedUomId, checkDate);
            if (cardResult.Found)
            {
                return cardResult;
            }

            // A Price Card was explicitly configured/assigned, but this product is not on it
            return new PriceResolutionResult
            {
                Found = false,
                ProductId = productId,
                PriceCardId = card.PriceCardId,
                PriceCardCode = card.PriceCardCode,
                PriceCardName = card.PriceCardName,
                PriceCardType = card.PriceCardType,
                Message = $"Product does not exist on the customer's assigned Price Card '{card.PriceCardCode}'."
            };
        }

        // Fallback: If no Sales Price Card is assigned to Customer, check Product Master SellingPrice
        var product = await _productRepository.GetByIdAsync(tenantId, productId);
        if (product != null && product.SellingPrice > 0)
        {
            decimal unitPrice = product.SellingPrice;
            Guid? resolvedUom = product.UnitOfMeasureId;

            if (requestedUomId.HasValue && product.UnitOfMeasureId.HasValue && requestedUomId.Value != product.UnitOfMeasureId.Value && _uomService != null)
            {
                try
                {
                    var conv = await _uomService.ConvertProductQuantityAsync(
                        tenantId,
                        productId,
                        requestedUomId.Value,
                        product.UnitOfMeasureId.Value,
                        1m,
                        ConversionContext.Sales);

                    if (conv != null && conv.ConvertedQuantity > 0)
                    {
                        unitPrice = Math.Round(product.SellingPrice * conv.ConvertedQuantity, 4);
                        resolvedUom = requestedUomId.Value;
                    }
                }
                catch { }
            }

            return new PriceResolutionResult
            {
                Found = true,
                ProductId = productId,
                ResolvedUomId = resolvedUom,
                UnitPrice = unitPrice,
                Currency = "USD",
                ExchangeRate = 1m,
                Source = "ProductMaster"
            };
        }

        return new PriceResolutionResult
        {
            Found = false,
            ProductId = productId,
            Message = "No sales price configured for this product and customer has no default sales price card."
        };
    }
}
