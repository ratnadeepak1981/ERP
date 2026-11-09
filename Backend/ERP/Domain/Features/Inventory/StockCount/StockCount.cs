using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Domain.Features.Inventory.StockCount;

public class StockCount
{
    public Guid StockCountId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public StockCountStatus Status { get; set; } = StockCountStatus.Draft;
    public string? Remarks { get; set; }
    public string? SnapshotFingerprint { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<StockCountLine> Lines { get; set; } = new();

    public decimal TotalExpectedQuantity => Lines.Sum(x => x.ExpectedQuantityBase);
    public decimal TotalPhysicalQuantity => Lines.Sum(x => x.PhysicalQuantityBase);
    public decimal TotalVarianceQuantity => Lines.Sum(x => x.VarianceQuantityBase);

    public void UpdateDraft()
    {
        if (Status != StockCountStatus.Draft)
        {
            throw new InvalidOperationException($"Stock count '{CountNumber}' cannot be updated while in status '{Status}'.");
        }

        UpdatedAt = DateTime.UtcNow;
        foreach (var line in Lines)
        {
            line.RecalculateVariance();
        }
    }

    public void Submit(Guid userId)
    {
        if (Status != StockCountStatus.Draft)
        {
            throw new InvalidOperationException($"Stock count '{CountNumber}' cannot be submitted while in status '{Status}'.");
        }

        Status = StockCountStatus.Submitted;
        SubmittedByUserId = userId;
        SubmittedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        foreach (var line in Lines)
        {
            line.RecalculateVariance();
        }
    }

    public void Approve(Guid userId)
    {
        if (Status != StockCountStatus.Submitted)
        {
            throw new InvalidOperationException($"Stock count '{CountNumber}' cannot be approved while in status '{Status}'.");
        }

        Status = StockCountStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Post(Guid userId)
    {
        if (Status != StockCountStatus.Approved)
        {
            throw new InvalidOperationException($"Stock count '{CountNumber}' cannot be posted while in status '{Status}'.");
        }

        Status = StockCountStatus.Posted;
        PostedByUserId = userId;
        PostedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(Guid userId, string? reason = null)
    {
        if (Status == StockCountStatus.Posted)
        {
            throw new InvalidOperationException($"Posted stock count '{CountNumber}' cannot be cancelled.");
        }

        Status = StockCountStatus.Cancelled;
        CancelledByUserId = userId;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public static StockCount Create(
        Guid tenantId,
        Guid companyId,
        Guid warehouseId,
        Guid warehouseLocationId,
        string countNumber,
        DateTime? countDate = null,
        string? remarks = null)
    {
        return new StockCount
        {
            StockCountId = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            CountNumber = countNumber,
            CountDate = countDate ?? DateTime.UtcNow,
            Remarks = remarks,
            Status = StockCountStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
