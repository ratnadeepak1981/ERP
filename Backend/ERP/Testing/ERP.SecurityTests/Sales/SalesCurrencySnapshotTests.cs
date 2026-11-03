using System;
using Domain.Features.MasterData.Currency;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.SalesOrder;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesCurrencySnapshotTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    [Fact]
    public void SalesOrder_SetCurrencySnapshot_ComputesBaseAmountsAccurately()
    {
        // Arrange
        var order = SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-EUR-001",
            currency: "EUR");

        // Add item: Qty 10 @ 50.00 EUR with 10% discount and 5% tax
        // Subtotal = 500.00 EUR
        // Discount = 50.00 EUR
        // Taxable = 450.00 EUR
        // Tax = 22.50 EUR
        // Total = 472.50 EUR
        order.AddItem(
            _productId,
            quantity: 10m,
            unitPrice: 50.00m,
            discountRate: 10m,
            taxRate: 5m);

        var txnCurrencyId = Guid.NewGuid();
        var baseCurrencyId = Guid.NewGuid();
        // EUR to USD rate: 1.0850
        var rate = new ResolvedExchangeRate(1.0850m, ExchangeRateSource.Daily, Guid.NewGuid(), DateTime.UtcNow);

        // Act
        order.SetCurrencySnapshot(
            txnCurrencyId,
            "EUR",
            baseCurrencyId,
            "USD",
            2,
            rate);

        // Assert
        Assert.Equal(txnCurrencyId, order.TransactionCurrencyId);
        Assert.Equal("EUR", order.Currency);
        Assert.Equal(baseCurrencyId, order.BaseCurrencyId);
        Assert.Equal("USD", order.BaseCurrencyCode);
        Assert.Equal(2, order.BaseCurrencyDecimalPlaces);
        Assert.Equal(1.0850m, order.AppliedExchangeRate);
        Assert.Equal(ExchangeRateSource.Daily, order.ExchangeRateSource);

        // 500.00 * 1.0850 = 542.50
        Assert.Equal(542.50m, order.BaseSubTotal);
        // 50.00 * 1.0850 = 54.25
        Assert.Equal(54.25m, order.BaseDiscountAmount);
        // 22.50 * 1.0850 = 24.4125 -> 24.41
        Assert.Equal(24.41m, order.BaseTaxAmount);
        // 472.50 * 1.0850 = 512.6625 -> 512.66
        Assert.Equal(512.66m, order.BaseTotalAmount);
    }

    [Fact]
    public void SalesOrder_RecalculateTotals_PreservesAndRecalculatesBaseAmounts()
    {
        // Arrange
        var order = SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-EUR-002",
            currency: "EUR");

        var txnCurrencyId = Guid.NewGuid();
        var baseCurrencyId = Guid.NewGuid();
        var rate = new ResolvedExchangeRate(1.1000m, ExchangeRateSource.Daily, Guid.NewGuid(), DateTime.UtcNow);

        order.SetCurrencySnapshot(txnCurrencyId, "EUR", baseCurrencyId, "USD", 2, rate);

        // Act: Add item after snapshot
        order.AddItem(
            _productId,
            quantity: 2m,
            unitPrice: 100.00m,
            discountRate: 0m,
            taxRate: 0m);

        // Assert: 200.00 EUR * 1.1000 = 220.00 USD
        Assert.Equal(200.00m, order.SubTotal);
        Assert.Equal(200.00m, order.TotalAmount);
        Assert.Equal(220.00m, order.BaseSubTotal);
        Assert.Equal(220.00m, order.BaseTotalAmount);
    }

    [Fact]
    public void SalesInvoice_SetCurrencySnapshot_ComputesBaseAmountsAccurately()
    {
        // Arrange
        var invoice = SalesInvoice.Create(
            _tenantId,
            _companyId,
            _branchId,
            Guid.NewGuid(),
            _customerId,
            "INV-EUR-001",
            currency: "EUR");

        var item = SalesInvoiceItem.Create(
            invoice.Id,
            _productId,
            quantity: 5m,
            unitPrice: 100.00m,
            discountRate: 0m,
            taxRate: 10m);

        invoice.AddItem(item);

        var txnCurrencyId = Guid.NewGuid();
        var baseCurrencyId = Guid.NewGuid();
        var rate = new ResolvedExchangeRate(1.2000m, ExchangeRateSource.Monthly, Guid.NewGuid(), DateTime.UtcNow);

        // Act
        invoice.SetCurrencySnapshot(
            txnCurrencyId,
            "EUR",
            baseCurrencyId,
            "USD",
            2,
            rate);

        // Assert: Subtotal 500, Tax 50, Total 550. Base @ 1.2: BaseSubTotal 600, BaseTax 60, BaseTotal 660.
        Assert.Equal(500.00m, invoice.Subtotal);
        Assert.Equal(50.00m, invoice.TaxAmount);
        Assert.Equal(550.00m, invoice.TotalAmount);
        Assert.Equal(600.00m, invoice.BaseSubTotal);
        Assert.Equal(60.00m, invoice.BaseTaxAmount);
        Assert.Equal(660.00m, invoice.BaseTotalAmount);
    }

    [Fact]
    public void SalesOrder_SetCurrencySnapshot_ThrowsWhenNotDraft()
    {
        // Arrange
        var order = SalesOrder.Create(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            "SO-EUR-003");

        order.AddItem(_productId, 1m, 10m);
        order.Submit();

        var rate = new ResolvedExchangeRate(1.0m, ExchangeRateSource.SameCurrency, null, DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            order.SetCurrencySnapshot(Guid.NewGuid(), "USD", Guid.NewGuid(), "USD", 2, rate));
    }

    [Fact]
    public void SalesInvoice_SetCurrencySnapshot_ThrowsWhenNotDraft()
    {
        // Arrange
        var invoice = SalesInvoice.Create(
            _tenantId,
            _companyId,
            _branchId,
            Guid.NewGuid(),
            _customerId,
            "INV-EUR-002");

        var item = SalesInvoiceItem.Create(invoice.Id, _productId, 1m, 10m);
        invoice.AddItem(item);
        invoice.MarkConfirmed(Guid.NewGuid());

        var rate = new ResolvedExchangeRate(1.0m, ExchangeRateSource.SameCurrency, null, DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            invoice.SetCurrencySnapshot(Guid.NewGuid(), "USD", Guid.NewGuid(), "USD", 2, rate));
    }
}
