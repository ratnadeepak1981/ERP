using System;
using System.Collections.Generic;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using Xunit;

namespace ERP.SecurityTests;

public class TenantBillingAndLedgerTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Pending, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Cancelled, true)]
    [InlineData(InvoiceStatus.Draft, InvoiceStatus.Paid, false)]
    [InlineData(InvoiceStatus.Pending, InvoiceStatus.PartiallyPaid, true)]
    [InlineData(InvoiceStatus.Pending, InvoiceStatus.Paid, true)]
    [InlineData(InvoiceStatus.Pending, InvoiceStatus.Overdue, true)]
    [InlineData(InvoiceStatus.Pending, InvoiceStatus.Failed, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, true)]
    [InlineData(InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue, true)]
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Pending, false)]
    [InlineData(InvoiceStatus.Paid, InvoiceStatus.Cancelled, false)]
    [InlineData(InvoiceStatus.Cancelled, InvoiceStatus.Pending, false)]
    public void InvoiceStatusTransitions_EnforcesPermittedAndForbiddenTransitions(
        InvoiceStatus current,
        InvoiceStatus target,
        bool expectedAllowed)
    {
        bool allowed = BillingRules.CanTransition(current, target);
        Assert.Equal(expectedAllowed, allowed);

        if (expectedAllowed)
        {
            BillingRules.ValidateInvoiceTransition(current, target);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
                BillingRules.ValidateInvoiceTransition(current, target));
        }
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Success, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Failed, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Refunded, false)]
    [InlineData(PaymentStatus.Success, PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Success, PaymentStatus.PartiallyRefunded, true)]
    [InlineData(PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Success, false)]
    [InlineData(PaymentStatus.Refunded, PaymentStatus.Success, false)]
    public void PaymentStatusTransitions_EnforcesPermittedAndForbiddenTransitions(
        PaymentStatus current,
        PaymentStatus target,
        bool expectedAllowed)
    {
        bool allowed = BillingRules.CanTransition(current, target);
        Assert.Equal(expectedAllowed, allowed);

        if (expectedAllowed)
        {
            BillingRules.ValidatePaymentTransition(current, target);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
                BillingRules.ValidatePaymentTransition(current, target));
        }
    }

    [Fact]
    public void SubscriptionInvoice_PartialPayments_UpdatesBalanceAndStateAccurately()
    {
        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            SubscriptionId = Guid.NewGuid(),
            InvoiceNumber = "INV-2026-0001",
            TotalAmount = 500.00m,
            PaidAmount = 0.00m,
            CreditedAmount = 0.00m,
            Status = InvoiceStatus.Pending
        };

        Assert.Equal(500.00m, invoice.OutstandingAmount);

        // 1. First partial payment: 200.00
        var now = DateTime.UtcNow;
        invoice.ApplyPayment(200.00m, now);

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(200.00m, invoice.PaidAmount);
        Assert.Equal(300.00m, invoice.OutstandingAmount);
        Assert.Null(invoice.PaidAt);

        // 2. Second partial payment: 150.00
        invoice.ApplyPayment(150.00m, now);

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(350.00m, invoice.PaidAmount);
        Assert.Equal(150.00m, invoice.OutstandingAmount);

        // 3. Final payment fulfilling invoice: 150.00
        invoice.ApplyPayment(150.00m, now);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(500.00m, invoice.PaidAmount);
        Assert.Equal(0.00m, invoice.OutstandingAmount);
        Assert.Equal(now, invoice.PaidAt);

        // 4. Overpayment attempt throws exception
        Assert.Throws<InvalidOperationException>(() =>
            invoice.ApplyPayment(10.00m, now));
    }

    [Fact]
    public void SubscriptionInvoice_CreditNotes_ReducesOutstandingBalance()
    {
        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            SubscriptionId = Guid.NewGuid(),
            InvoiceNumber = "INV-2026-0002",
            TotalAmount = 1000.00m,
            PaidAmount = 600.00m,
            CreditedAmount = 0.00m,
            Status = InvoiceStatus.PartiallyPaid
        };

        Assert.Equal(400.00m, invoice.OutstandingAmount);

        // Apply credit note of 400.00
        invoice.ApplyCredit(400.00m);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(400.00m, invoice.CreditedAmount);
        Assert.Equal(0.00m, invoice.OutstandingAmount);
    }

    [Fact]
    public void PaymentAllocation_MultiInvoiceAndCrossTenantProtection()
    {
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            PaymentProvider = "DummyGateway",
            ProviderTransactionId = "TXN-1001",
            IdempotencyKey = "IDEMP-1001",
            Amount = 1000.00m,
            AllocatedAmount = 0.00m,
            Status = PaymentStatus.Success
        };

        var invoiceA1 = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            TotalAmount = 600.00m,
            PaidAmount = 0.00m,
            Status = InvoiceStatus.Pending
        };

        var invoiceA2 = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            TotalAmount = 800.00m,
            PaidAmount = 0.00m,
            Status = InvoiceStatus.Pending
        };

        var invoiceTenantB = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB,
            TotalAmount = 500.00m,
            PaidAmount = 0.00m,
            Status = InvoiceStatus.Pending
        };

        // 1. Cross-tenant allocation attempt MUST throw
        var crossTenantEx = Assert.Throws<InvalidOperationException>(() =>
            BillingRules.ValidateAllocation(payment, invoiceTenantB, 500.00m));
        Assert.Contains("Cross-tenant payment allocation is strictly forbidden", crossTenantEx.Message);

        // 2. Allocate 600 to invoice A1
        BillingRules.ValidateAllocation(payment, invoiceA1, 600.00m);
        payment.AllocatedAmount += 600.00m;
        invoiceA1.ApplyPayment(600.00m, DateTime.UtcNow);

        Assert.Equal(400.00m, payment.UnallocatedAmount);
        Assert.Equal(InvoiceStatus.Paid, invoiceA1.Status);

        // 3. Allocate remaining 400 to invoice A2
        BillingRules.ValidateAllocation(payment, invoiceA2, 400.00m);
        payment.AllocatedAmount += 400.00m;
        invoiceA2.ApplyPayment(400.00m, DateTime.UtcNow);

        Assert.Equal(0.00m, payment.UnallocatedAmount);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoiceA2.Status);
        Assert.Equal(400.00m, invoiceA2.OutstandingAmount);

        // 4. Over-allocation attempt MUST throw
        Assert.Throws<InvalidOperationException>(() =>
            BillingRules.ValidateAllocation(payment, invoiceA2, 100.00m));
    }

    [Fact]
    public void FailedPayment_DoesNotReduceOutstandingBalances()
    {
        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            PaymentProvider = "DummyGateway",
            ProviderTransactionId = "TXN-FAILED-1",
            IdempotencyKey = "IDEMP-FAIL-1",
            Amount = 500.00m,
            AllocatedAmount = 0.00m,
            Status = PaymentStatus.Failed
        };

        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            TotalAmount = 500.00m,
            PaidAmount = 0.00m,
            Status = InvoiceStatus.Pending
        };

        // UnallocatedAmount on failed payment is 0
        Assert.Equal(0.00m, payment.UnallocatedAmount);

        // Allocation validation throws because status is Failed
        var ex = Assert.Throws<InvalidOperationException>(() =>
            BillingRules.ValidateAllocation(payment, invoice, 500.00m));
        Assert.Contains("Payment must be in Success status to allocate funds", ex.Message);

        // Invoice balance remains unchanged
        Assert.Equal(500.00m, invoice.OutstandingAmount);
        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
    }

    [Fact]
    public void BillingLedger_CalculatesTenantReceivableBalance_AndReversalsAccurately()
    {
        var tenantId = TenantA;
        var ledgerEntries = new List<BillingLedgerEntry>
        {
            // 1. Invoice posted: Debit 1200.00 (Tenant owes 1200.00)
            new BillingLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntryType = LedgerEntryType.InvoiceCharge,
                Direction = LedgerDirection.Debit,
                Amount = 1200.00m,
                PostedAtUtc = DateTime.UtcNow.AddDays(-10)
            },
            // 2. Payment received: Credit 1000.00 (Reduces debt to 200.00)
            new BillingLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntryType = LedgerEntryType.PaymentReceived,
                Direction = LedgerDirection.Credit,
                Amount = 1000.00m,
                PostedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            // 3. Credit note issued: Credit 150.00 (Reduces debt to 50.00)
            new BillingLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntryType = LedgerEntryType.CreditNoteIssued,
                Direction = LedgerDirection.Credit,
                Amount = 150.00m,
                PostedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            // 4. Refund processed on earlier payment: Debit 200.00 (Reverses part of payment; debt becomes 250.00)
            new BillingLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntryType = LedgerEntryType.RefundIssued,
                Direction = LedgerDirection.Debit,
                Amount = 200.00m,
                PostedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        };

        decimal receivableBalance = BillingRules.CalculateTenantReceivableBalance(ledgerEntries);

        // 1200 - 1000 - 150 + 200 = 250.00
        Assert.Equal(250.00m, receivableBalance);
    }
}
