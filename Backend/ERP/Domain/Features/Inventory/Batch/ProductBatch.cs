using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.Inventory.Batch;

public class ProductBatch : Auditable, ITenantScopedEntity
{
    public Guid ProductBatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string BatchNumber { get; private set; } = string.Empty;
    public DateTime? ManufacturingDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ProductBatch Create(
        Guid tenantId,
        Guid productId,
        string batchNumber,
        DateTime? manufacturingDate = null,
        DateTime? expiryDate = null,
        Guid? productBatchId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(batchNumber))
            throw new ArgumentException("BatchNumber is required.", nameof(batchNumber));

        if (manufacturingDate.HasValue && expiryDate.HasValue && expiryDate.Value.Date < manufacturingDate.Value.Date)
        {
            throw new InvalidOperationException(
                $"Expiry date ({expiryDate.Value:yyyy-MM-dd}) cannot be earlier than manufacturing date ({manufacturingDate.Value:yyyy-MM-dd}).");
        }

        return new ProductBatch
        {
            ProductBatchId = productBatchId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            BatchNumber = batchNumber.Trim(),
            ManufacturingDate = manufacturingDate?.Date,
            ExpiryDate = expiryDate?.Date,
            IsActive = true
        };
    }

    public void UpdateDates(DateTime? manufacturingDate, DateTime? expiryDate)
    {
        if (manufacturingDate.HasValue && expiryDate.HasValue && expiryDate.Value.Date < manufacturingDate.Value.Date)
        {
            throw new InvalidOperationException(
                $"Expiry date ({expiryDate.Value:yyyy-MM-dd}) cannot be earlier than manufacturing date ({manufacturingDate.Value:yyyy-MM-dd}).");
        }

        if (manufacturingDate.HasValue)
        {
            ManufacturingDate = manufacturingDate.Value.Date;
        }

        if (expiryDate.HasValue)
        {
            ExpiryDate = expiryDate.Value.Date;
        }
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private ProductBatch()
    {
    }
}
