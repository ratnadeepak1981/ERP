using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Supplier;

public class SupplierProductPriceService : ISupplierProductPriceService
{
    private readonly ISupplierProductPriceRepository _repository;

    public SupplierProductPriceService(ISupplierProductPriceRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SupplierProductPrice>> GetPricesAsync(Guid tenantId, Guid? supplierId = null, Guid? productId = null)
    {
        return await _repository.GetPricesAsync(tenantId, supplierId, productId);
    }

    public async Task<SupplierProductPrice?> GetPriceAsync(Guid tenantId, Guid priceId)
    {
        return await _repository.GetPriceByIdAsync(tenantId, priceId);
    }

    public async Task<SupplierProductPrice> CreatePriceAsync(Guid tenantId, CreateSupplierProductPriceRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (!await _repository.SupplierExistsAsync(tenantId, request.SupplierId))
            throw new InvalidOperationException("Supplier not found or does not belong to the current tenant.");

        if (!await _repository.ProductExistsAsync(tenantId, request.ProductId))
            throw new InvalidOperationException("Product not found or does not belong to the current tenant.");

        if (request.UnitOfMeasureId.HasValue && !await _repository.UnitOfMeasureExistsAsync(tenantId, request.UnitOfMeasureId.Value))
            throw new InvalidOperationException("Unit of measure not found or does not belong to the current tenant.");

        var price = SupplierProductPrice.Create(
            tenantId,
            request.SupplierId,
            request.ProductId,
            request.UnitPrice,
            request.EffectiveFrom,
            request.Currency,
            request.SupplierItemCode?.Trim(),
            request.UnitOfMeasureId,
            request.MinimumOrderQuantity,
            request.EffectiveTo,
            request.LeadTimeDays,
            request.IsPreferred);

        await _repository.AddPriceAsync(price);
        await _repository.SaveChangesAsync();
        return price;
    }

    public async Task<SupplierProductPrice> UpdatePriceAsync(Guid tenantId, Guid priceId, UpdateSupplierProductPriceRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var price = await _repository.GetPriceByIdAsync(tenantId, priceId)
            ?? throw new KeyNotFoundException("Supplier product price card not found.");

        if (request.UnitOfMeasureId.HasValue && !await _repository.UnitOfMeasureExistsAsync(tenantId, request.UnitOfMeasureId.Value))
            throw new InvalidOperationException("Unit of measure not found or does not belong to the current tenant.");

        price.Update(
            request.UnitPrice,
            request.EffectiveFrom,
            request.Currency,
            request.SupplierItemCode?.Trim(),
            request.UnitOfMeasureId,
            request.MinimumOrderQuantity,
            request.EffectiveTo,
            request.LeadTimeDays,
            request.IsPreferred);

        await _repository.SaveChangesAsync();
        return price;
    }

    public async Task DeactivatePriceAsync(Guid tenantId, Guid priceId)
    {
        var price = await _repository.GetPriceByIdAsync(tenantId, priceId)
            ?? throw new KeyNotFoundException("Supplier product price card not found.");

        price.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<SupplierProductPrice?> GetActivePriceAsync(Guid tenantId, Guid supplierId, Guid productId, DateTime? asOfDate = null)
    {
        DateTime checkDate = asOfDate ?? DateTime.UtcNow;
        return await _repository.GetActivePriceAsync(tenantId, supplierId, productId, checkDate);
    }
}
