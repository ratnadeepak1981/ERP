using System;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Sales.SalesOrder;
using Xunit;

namespace ERP.SecurityTests.MasterData;

public class TaxCalculationDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    [Fact]
    public void PurchaseOrder_TaxExclusive_WithDiscount_CalculatesCorrectly()
    {
        // 10 units @ $100.00 each = $1,000.00 gross
        // 10% discount = $100.00 discount
        // Taxable amount = $900.00
        // 15% VAT = $135.00
        // Line total = $1,035.00
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-TAX-001");
        var taxCodeId = Guid.NewGuid();

        var item = po.AddItem(
            _productId,
            quantity: 10m,
            unitPrice: 100m,
            unitOfMeasureId: null,
            discountRate: 10m,
            taxCodeId: taxCodeId,
            taxRate: 15m,
            isTaxInclusive: false);

        Assert.Equal(1000.00m, item.SubTotal);
        Assert.Equal(100.00m, item.DiscountAmount);
        Assert.Equal(15.00m, item.TaxRate);
        Assert.Equal(135.00m, item.TaxAmount);
        Assert.Equal(1035.00m, item.LineTotal);
        Assert.False(item.IsTaxInclusive);
        Assert.Equal(taxCodeId, item.TaxCodeId);

        // Header summaries
        Assert.Equal(1000.00m, po.SubTotal);
        Assert.Equal(100.00m, po.DiscountAmount);
        Assert.Equal(135.00m, po.TaxAmount);
        Assert.Equal(1035.00m, po.TotalAmount);
    }

    [Fact]
    public void PurchaseOrder_TaxInclusive_WithDiscount_CalculatesCorrectly()
    {
        // 1 unit @ $115.00 inclusive price = $115.00 gross
        // 0% discount
        // Taxable basis = $115.00 / 1.15 = $100.00
        // Tax amount = $115.00 - $100.00 = $15.00
        // Line total = $115.00
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-TAX-002");

        var item = po.AddItem(
            _productId,
            quantity: 1m,
            unitPrice: 115m,
            unitOfMeasureId: null,
            discountRate: 0m,
            taxCodeId: null,
            taxRate: 15m,
            isTaxInclusive: true);

        Assert.Equal(115.00m, item.SubTotal);
        Assert.Equal(0.00m, item.DiscountAmount);
        Assert.Equal(15.00m, item.TaxAmount);
        Assert.Equal(115.00m, item.LineTotal);
        Assert.True(item.IsTaxInclusive);

        Assert.Equal(115.00m, po.SubTotal);
        Assert.Equal(15.00m, po.TaxAmount);
        Assert.Equal(115.00m, po.TotalAmount);
    }

    [Fact]
    public void SalesOrder_TaxExclusive_WithDiscount_CalculatesCorrectly()
    {
        var so = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-TAX-001");
        var taxCodeId = Guid.NewGuid();

        var item = so.AddItem(
            _productId,
            quantity: 5m,
            unitPrice: 50m,
            discountRate: 5m,
            taxRate: 10m,
            notes: "Test sale line",
            taxCodeId: taxCodeId,
            isTaxInclusive: false);

        // Gross = 5 * 50 = 250.00
        // Discount = 250 * 5% = 12.50
        // Taxable = 250 - 12.50 = 237.50
        // Tax = 237.50 * 10% = 23.75
        // LineTotal = 237.50 + 23.75 = 261.25
        Assert.Equal(250.00m, item.SubTotal);
        Assert.Equal(12.50m, item.DiscountAmount);
        Assert.Equal(23.75m, item.TaxAmount);
        Assert.Equal(261.25m, item.LineTotal);
        Assert.Equal(taxCodeId, item.TaxCodeId);
        Assert.False(item.IsTaxInclusive);

        Assert.Equal(250.00m, so.SubTotal);
        Assert.Equal(12.50m, so.DiscountAmount);
        Assert.Equal(23.75m, so.TaxAmount);
        Assert.Equal(261.25m, so.TotalAmount);
    }

    [Fact]
    public void SalesOrder_TaxInclusive_WithDiscount_CalculatesCorrectly()
    {
        var so = SalesOrder.Create(_tenantId, _companyId, _branchId, _customerId, "SO-TAX-002");

        // 2 units @ $115.00 inclusive = $230.00 gross
        // 10% discount = $23.00 discount
        // Discounted gross = $207.00
        // Taxable basis = $207.00 / 1.15 = $180.00
        // Tax amount = $207.00 - $180.00 = $27.00
        // LineTotal = $207.00
        var item = so.AddItem(
            _productId,
            quantity: 2m,
            unitPrice: 115m,
            discountRate: 10m,
            taxRate: 15m,
            notes: "Inclusive test",
            isTaxInclusive: true);

        Assert.Equal(230.00m, item.SubTotal);
        Assert.Equal(23.00m, item.DiscountAmount);
        Assert.Equal(27.00m, item.TaxAmount);
        Assert.Equal(207.00m, item.LineTotal);
        Assert.True(item.IsTaxInclusive);

        Assert.Equal(230.00m, so.SubTotal);
        Assert.Equal(23.00m, so.DiscountAmount);
        Assert.Equal(27.00m, so.TaxAmount);
        Assert.Equal(207.00m, so.TotalAmount);
    }

    [Fact]
    public void HistoricalSnapshot_PreservedEvenIfTaxCodeChangesOrDeactivates()
    {
        // 1. Setup master tax code
        var taxCode = TaxCode.Create(_tenantId, "VAT15", "Value Added Tax 15%", 15.00m);

        // 2. Create order referencing taxCode
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-HIST-001");
        var item = po.AddItem(_productId, quantity: 10m, unitPrice: 100m, taxCodeId: taxCode.TaxCodeId, taxRate: taxCode.Rate);

        Assert.Equal(15.00m, item.TaxRate);
        Assert.Equal(150.00m, item.TaxAmount);
        Assert.Equal(1150.00m, item.LineTotal);

        // 3. Mutate master tax code (rate increased to 20% and deactivated)
        taxCode.Update("VAT 20%", 20.00m, TaxApplicability.Both, null, null, "Rate increased");
        taxCode.Deactivate();

        // 4. Historical PO item snapshot must remain intact with 15% and $150.00 tax
        Assert.Equal(15.00m, item.TaxRate);
        Assert.Equal(150.00m, item.TaxAmount);
        Assert.Equal(1150.00m, item.LineTotal);
        Assert.Equal(1150.00m, po.TotalAmount);
    }

    [Fact]
    public void TaxCode_ApplicabilityAndEffectiveDate_MethodsBehaveExpectedly()
    {
        var baseDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var taxCode = TaxCode.Create(
            _tenantId,
            "PURCHASE_ONLY",
            "Purchase Only Tax",
            10.0m,
            applicability: TaxApplicability.Purchase,
            effectiveFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveTo: new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.True(taxCode.AppliesTo(TaxApplicability.Purchase));
        Assert.False(taxCode.AppliesTo(TaxApplicability.Sales));

        // Effective date
        Assert.True(taxCode.IsEffectiveAt(baseDate));
        Assert.False(taxCode.IsEffectiveAt(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(taxCode.IsEffectiveAt(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        // Deactivated
        taxCode.Deactivate();
        Assert.False(taxCode.IsEffectiveAt(baseDate));
    }
}
