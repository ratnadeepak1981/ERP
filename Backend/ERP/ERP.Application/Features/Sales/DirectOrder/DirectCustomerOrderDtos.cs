using System;
using System.Collections.Generic;

namespace ERP.Application.Features.Sales.DirectOrder;

public sealed class DirectB2BOrderLineRequest
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateDirectB2BOrderRequest
{
    public Guid SellingCompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DeliveryAddressId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CustomerRemarks { get; set; }
    public List<DirectB2BOrderLineRequest> Items { get; set; } = new();
}
