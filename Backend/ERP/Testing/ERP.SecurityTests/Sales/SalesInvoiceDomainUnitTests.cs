using System;
using Domain.Features.Sales.Invoice;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesInvoiceDomainUnitTests
{
    [Fact]
    public void SalesInvoice_Create_SetsExpectedInitialValues()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        var invoice = SalesInvoice.Create(
            tenantId,
            companyId,
            branchId,
            orderId,
            customerId,
            "INV-2026-001",
            salesShipmentId: shipmentId,
            currency: "USD",
            notes: "Initial test invoice");

        Assert.Equal(tenantId, invoice.TenantId);
        Assert.Equal(companyId, invoice.CompanyId);
        Assert.Equal(branchId, invoice.BranchId);
        Assert.Equal(customerId, invoice.CustomerId);
        Assert.Equal("INV-2026-001", invoice.InvoiceNumber);
        Assert.Equal("USD", invoice.Currency);
        Assert.Equal(SalesInvoiceStatus.Draft, invoice.Status);
        Assert.Equal(orderId, invoice.SalesOrderId);
        Assert.Equal(shipmentId, invoice.SalesShipmentId);
        Assert.Equal(0, invoice.TotalAmount);
        Assert.True(invoice.IsActive);
        Assert.Empty(invoice.Items);
    }

    [Fact]
    public void SalesInvoice_AddItem_RecalculatesTotals()
    {
        var invoice = CreateTestInvoice();

        var item1 = SalesInvoiceItem.Create(
            invoice.Id,
            Guid.NewGuid(),
            quantity: 2,
            unitPrice: 100m,
            discountRate: 10m,
            taxRate: 15m);

        invoice.AddItem(item1);

        // Subtotal: 2 * 100 = 200
        // Discount: 200 * 0.10 = 20
        // After discount: 180
        // Tax: 180 * 0.15 = 27
        // Line total: 180 + 27 = 207
        Assert.Single(invoice.Items);
        Assert.Equal(200m, invoice.Subtotal);
        Assert.Equal(20m, invoice.DiscountAmount);
        Assert.Equal(27m, invoice.TaxAmount);
        Assert.Equal(207m, invoice.TotalAmount);
    }

    [Fact]
    public void SalesInvoice_Confirm_TransitionsStatusFromDraftToConfirmed()
    {
        var invoice = CreateTestInvoice();
        var item = SalesInvoiceItem.Create(
            invoice.Id,
            Guid.NewGuid(),
            quantity: 1,
            unitPrice: 50m);
        invoice.AddItem(item);

        Assert.Equal(SalesInvoiceStatus.Draft, invoice.Status);
        invoice.MarkConfirmed(Guid.NewGuid());

        Assert.Equal(SalesInvoiceStatus.Confirmed, invoice.Status);

        // Cannot add items or edit when confirmed
        var item2 = SalesInvoiceItem.Create(invoice.Id, Guid.NewGuid(), quantity: 1, unitPrice: 100m);
        Assert.Throws<InvalidOperationException>(() => invoice.AddItem(item2));
    }

    [Fact]
    public void SalesInvoice_Cancel_OnlyAllowedFromDraftOrConfirmed()
    {
        var invoice = CreateTestInvoice();
        invoice.MarkCancelled();

        Assert.Equal(SalesInvoiceStatus.Cancelled, invoice.Status);

        // Cannot confirm a cancelled invoice
        Assert.Throws<InvalidOperationException>(() => invoice.MarkConfirmed(Guid.NewGuid()));
    }

    private static SalesInvoice CreateTestInvoice()
    {
        return SalesInvoice.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "INV-TEST-01",
            currency: "USD");
    }
}
