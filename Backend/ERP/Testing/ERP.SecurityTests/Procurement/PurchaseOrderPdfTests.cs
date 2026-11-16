using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ERP.Application.Features.Procurement.PurchaseOrderFeature;
using ERP.Infrastructure.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SecurityTests.Procurement;

public class PurchaseOrderPdfTests
{
    private readonly PurchaseOrderPdfService _pdfService;

    public PurchaseOrderPdfTests()
    {
        // Null dbContext for pure model-based generation unit test
        _pdfService = new PurchaseOrderPdfService(null!, NullLogger<PurchaseOrderPdfService>.Instance);
    }

    [Fact]
    public void GeneratePdf_SinglePageOrder_ProducesValidPdf14Binary()
    {
        // Arrange
        var model = CreateSampleModel(itemCount: 5);

        // Act
        byte[] pdfBytes = _pdfService.GeneratePdf(model);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 2000, "PDF should have substantial size with styles and text.");

        string pdfText = Encoding.ASCII.GetString(pdfBytes);

        // Verify PDF standard header
        Assert.StartsWith("%PDF-1.4", pdfText);

        // Verify PDF standard EOF trailer
        Assert.Contains("%%EOF", pdfText);
        Assert.Contains("trailer", pdfText);
        Assert.Contains("startxref", pdfText);

        // Verify standard font dictionaries
        Assert.Contains("/BaseFont /Helvetica", pdfText);
        Assert.Contains("/BaseFont /Helvetica-Bold", pdfText);

        // Verify content elements
        Assert.Contains("PO-2026-11-0089", pdfText);
        Assert.Contains("Lanka Organic Agro Exports", pdfText);
        Assert.Contains("Highland Packaging Ltd", pdfText);
        Assert.Contains("APPROVED", pdfText);

        // Verify single page count
        Assert.Contains("/Count 1", pdfText);

        // Save a preview file for inspection
        var previewPath = Path.Combine(AppContext.BaseDirectory, "PurchaseOrder_SinglePage_Sample.pdf");
        File.WriteAllBytes(previewPath, pdfBytes);
    }

    [Fact]
    public void GeneratePdf_MultiPageOrder_CreatesMultiplePagesCleanly()
    {
        // Arrange: 30 items will exceed a single A4 page and require pagination
        var model = CreateSampleModel(itemCount: 30);

        // Act
        byte[] pdfBytes = _pdfService.GeneratePdf(model);

        // Assert
        Assert.NotNull(pdfBytes);
        string pdfText = Encoding.ASCII.GetString(pdfBytes);

        Assert.StartsWith("%PDF-1.4", pdfText);
        Assert.Contains("%%EOF", pdfText);

        // Verify multi-page catalog
        Assert.True(pdfText.Contains("/Count 2") || pdfText.Contains("/Count 3"), "Should have at least 2 pages.");

        // Verify item 1 and item 30 exist
        Assert.Contains("Item 1", pdfText);
        Assert.Contains("Item 30", pdfText);

        // Verify continued page text
        Assert.Contains("Page 2 of", pdfText);
    }

    private static PurchaseOrderPdfModel CreateSampleModel(int itemCount)
    {
        var model = new PurchaseOrderPdfModel
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "PO-2026-11-0089",
            OrderDate = new DateTime(2026, 11, 16),
            ExpectedDeliveryDate = new DateTime(2026, 11, 25),
            Status = "Approved",
            Currency = "LKR",
            PaymentTerms = "Net 30 Days",
            Remarks = "Handle with care. Delivery to Central Warehouse Bay 4.",
            CompanyName = "Lanka Organic Agro Exports (Pvt) Ltd",
            CompanyCode = "LOAE",
            BranchName = "Colombo Central Hub",
            CompanyAddress = "124 Port Road, Colombo 01",
            CompanyPhone = "+94 11 234 5678",
            CompanyEmail = "procurement@lankaagro.local",
            SupplierName = "Highland Packaging Ltd",
            SupplierCode = "SUP-HIGHLAND",
            SupplierContactPerson = "Nimal Fernando",
            SupplierEmail = "orders@highlandpkg.local",
            SupplierPhone = "+94 33 221 4455",
            SupplierAddress = "Export Processing Zone, Biyagama"
        };

        decimal subtotal = 0;
        for (int i = 1; i <= itemCount; i++)
        {
            decimal qty = 10 * i;
            decimal price = 250.00m;
            decimal lineTotal = qty * price;
            subtotal += lineTotal;

            model.Items.Add(new PurchaseOrderPdfLineItem
            {
                LineNumber = i,
                ProductCode = $"PRD-{i:D3}",
                ProductName = $"Packaging Material Item {i}",
                UnitOfMeasure = "BOX",
                Quantity = qty,
                UnitPrice = price,
                DiscountAmount = 0,
                TaxAmount = 0,
                LineTotal = lineTotal
            });
        }

        model.SubTotal = subtotal;
        model.DiscountAmount = 0;
        model.TaxAmount = Math.Round(subtotal * 0.15m, 2);
        model.TotalAmount = model.SubTotal + model.TaxAmount;

        return model;
    }
}
