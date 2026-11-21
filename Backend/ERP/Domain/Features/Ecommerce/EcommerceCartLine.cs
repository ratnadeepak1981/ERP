using System;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;

namespace Domain.Features.Ecommerce;

/// <summary>
/// A line item within an E-commerce Shopping Cart.
/// Line items persist requested quantities and packaging/UOM selections.
/// Server-side real-time pricing and tax rates are evaluated dynamically on demand.
/// </summary>
public class EcommerceCartLine
{
    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? UnitOfMeasureId { get; private set; }

    public Guid? ProductPackagingId { get; private set; }

    public decimal Quantity { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    // Navigation properties
    public EcommerceCart? Cart { get; private set; }

    public Product? Product { get; private set; }

    public UnitOfMeasure? UnitOfMeasure { get; private set; }

    public ProductPackaging? ProductPackaging { get; private set; }

    public static EcommerceCartLine Create(
        Guid cartId,
        Guid productId,
        decimal quantity,
        Guid? unitOfMeasureId = null,
        Guid? productPackagingId = null,
        Guid? id = null)
    {
        if (cartId == Guid.Empty)
            throw new ArgumentException("CartId is required.", nameof(cartId));

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        var now = DateTime.UtcNow;

        return new EcommerceCartLine
        {
            Id = id ?? Guid.NewGuid(),
            CartId = cartId,
            ProductId = productId,
            Quantity = quantity,
            UnitOfMeasureId = unitOfMeasureId,
            ProductPackagingId = productPackagingId,
            CreatedAtUtc = now,
            UpdatedAtUtc = null
        };
    }

    public void UpdateQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        Quantity = quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private EcommerceCartLine() { }
}
