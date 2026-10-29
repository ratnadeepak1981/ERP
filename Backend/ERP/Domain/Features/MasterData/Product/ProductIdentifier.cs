using System;
using Domain.Features.MasterData.Common;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Product;

public class ProductIdentifier : Auditable, ITenantScopedEntity
{
    public Guid ProductIdentifierId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? UnitOfMeasureId { get; private set; }
    public string IdentifierValue { get; private set; } = string.Empty;
    public IdentifierType IdentifierType { get; private set; }
    public IdentifierSymbology Symbology { get; private set; }
    public IdentifierSource Source { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ProductIdentifier Create(
        Guid tenantId,
        Guid productId,
        string identifierValue,
        IdentifierType identifierType,
        IdentifierSymbology symbology,
        IdentifierSource source,
        Guid? unitOfMeasureId = null,
        bool isPrimary = false,
        Guid? productIdentifierId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(identifierValue))
            throw new ArgumentException("Identifier value cannot be empty.", nameof(identifierValue));

        string normalized = identifierValue.Trim().ToUpperInvariant();

        return new ProductIdentifier
        {
            ProductIdentifierId = productIdentifierId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            IdentifierValue = normalized,
            IdentifierType = identifierType,
            Symbology = symbology,
            Source = source,
            IsPrimary = isPrimary,
            IsActive = true
        };
    }

    public void PromoteToPrimary()
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot set an inactive identifier as primary.");
        IsPrimary = true;
    }

    public void DemoteFromPrimary()
    {
        IsPrimary = false;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsPrimary = false;
    }

    public void Reactivate(bool isPrimary = false)
    {
        IsActive = true;
        IsPrimary = isPrimary;
    }

    private ProductIdentifier()
    {
    }
}
