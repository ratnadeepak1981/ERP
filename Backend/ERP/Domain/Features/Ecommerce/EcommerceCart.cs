using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Features.MasterData.Company;
using ERP.Domain.Features.MasterData.Customer;
using Domain.Features.Sales.SalesOrder;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Ecommerce;

public enum EcommerceCartStatus
{
    Active = 1,
    CheckedOut = 2,
    Abandoned = 3
}

/// <summary>
/// Persistent E-commerce Shopping Cart scoped per Tenant and Selling Company.
/// Supports both authenticated customers and anonymous guest sessions.
/// </summary>
public class EcommerceCart : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    private readonly List<EcommerceCartLine> _lines = new();

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? CustomerContactId { get; private set; }

    public string? SessionId { get; private set; }

    public EcommerceCartStatus Status { get; private set; }

    public Guid? SalesOrderId { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTime? CheckoutAttemptedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    // Navigation properties
    public Company? Company { get; private set; }

    public Customer? Customer { get; private set; }

    public SalesOrder? SalesOrder { get; private set; }

    public IReadOnlyCollection<EcommerceCartLine> Lines => _lines.AsReadOnly();

    public static EcommerceCart Create(
        Guid tenantId,
        Guid companyId,
        Guid? customerId = null,
        Guid? customerContactId = null,
        string? sessionId = null,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));

        if (!customerId.HasValue && string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Either CustomerId or SessionId must be provided.", nameof(sessionId));

        var now = DateTime.UtcNow;

        return new EcommerceCart
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CustomerId = customerId,
            CustomerContactId = customerContactId,
            SessionId = sessionId?.Trim(),
            Status = EcommerceCartStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = null
        };
    }

    public void AssociateCustomer(Guid customerId, Guid? customerContactId = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId is required.", nameof(customerId));

        CustomerId = customerId;
        CustomerContactId = customerContactId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public EcommerceCartLine AddOrUpdateLine(
        Guid productId,
        decimal quantity,
        Guid? unitOfMeasureId = null,
        Guid? productPackagingId = null)
    {
        if (Status != EcommerceCartStatus.Active)
            throw new InvalidOperationException($"Cannot modify a cart with status {Status}.");

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        var existingLine = _lines.FirstOrDefault(l =>
            l.ProductId == productId &&
            l.UnitOfMeasureId == unitOfMeasureId &&
            l.ProductPackagingId == productPackagingId);

        if (existingLine != null)
        {
            existingLine.UpdateQuantity(existingLine.Quantity + quantity);
            UpdatedAtUtc = DateTime.UtcNow;
            return existingLine;
        }

        var newLine = EcommerceCartLine.Create(Id, productId, quantity, unitOfMeasureId, productPackagingId);
        _lines.Add(newLine);
        UpdatedAtUtc = DateTime.UtcNow;
        return newLine;
    }

    public void UpdateLineQuantity(Guid lineId, decimal quantity)
    {
        if (Status != EcommerceCartStatus.Active)
            throw new InvalidOperationException($"Cannot modify a cart with status {Status}.");

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            throw new KeyNotFoundException($"Cart line '{lineId}' was not found.");

        if (quantity <= 0)
        {
            _lines.Remove(line);
        }
        else
        {
            line.UpdateQuantity(quantity);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveLine(Guid lineId)
    {
        if (Status != EcommerceCartStatus.Active)
            throw new InvalidOperationException($"Cannot modify a cart with status {Status}.");

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line != null)
        {
            _lines.Remove(line);
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void ClearLines()
    {
        if (Status != EcommerceCartStatus.Active)
            throw new InvalidOperationException($"Cannot modify a cart with status {Status}.");

        _lines.Clear();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordCheckoutAttempt(string idempotencyKey)
    {
        if (Status != EcommerceCartStatus.Active)
            throw new InvalidOperationException($"Cannot checkout a cart with status {Status}.");

        IdempotencyKey = idempotencyKey?.Trim();
        CheckoutAttemptedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCheckedOut(Guid salesOrderId, string? idempotencyKey = null)
    {
        if (salesOrderId == Guid.Empty)
            throw new ArgumentException("SalesOrderId is required.", nameof(salesOrderId));

        Status = EcommerceCartStatus.CheckedOut;
        SalesOrderId = salesOrderId;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            IdempotencyKey = idempotencyKey.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAbandoned()
    {
        Status = EcommerceCartStatus.Abandoned;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private EcommerceCart() { }
}
