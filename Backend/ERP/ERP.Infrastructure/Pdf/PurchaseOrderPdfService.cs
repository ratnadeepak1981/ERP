using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Procurement.PurchaseOrderFeature;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Pdf;

/// <summary>
/// Standards-compliant ISO 32000-1 (PDF 1.4) generator for Purchase Orders.
/// Operates with pure .NET BCL (zero external NuGet dependencies), produces valid PDF binary streams,
/// and handles multi-page line item overflows, professional business headers, and financial summaries.
/// </summary>
public class PurchaseOrderPdfService : IPurchaseOrderPdfService
{
    private readonly DomainDbContext _dbContext;
    private readonly ILogger<PurchaseOrderPdfService> _logger;

    // A4 dimensions in typographic points (1/72 inch)
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double MarginLeft = 36.0;
    private const double MarginRight = 559.28; // PageWidth - 36.0
    private const double ContentWidth = 523.28;

    public PurchaseOrderPdfService(DomainDbContext dbContext, ILogger<PurchaseOrderPdfService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePdfForOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId && o.CompanyId == companyId, cancellationToken);

        if (order == null)
            throw new KeyNotFoundException($"Purchase order '{orderId}' not found in authorized scope.");

        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId && c.TenantId == tenantId, cancellationToken);

        var branch = await _dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == order.BranchId && b.CompanyId == companyId, cancellationToken);

        Domain.Features.MasterData.Supplier.Supplier? supplier = null;
        if (order.SupplierId.HasValue)
        {
            supplier = await _dbContext.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == order.SupplierId.Value && s.TenantId == tenantId, cancellationToken);
        }

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.ProductId) && p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.ProductId, p => p, cancellationToken);

        var uomIds = order.Items.Where(i => i.UnitOfMeasureId.HasValue).Select(i => i.UnitOfMeasureId!.Value).Distinct().ToList();
        var uoms = await _dbContext.UnitsOfMeasure
            .Where(u => uomIds.Contains(u.UnitOfMeasureId) && u.TenantId == tenantId)
            .ToDictionaryAsync(u => u.UnitOfMeasureId, u => u.Code, cancellationToken);

        var model = new PurchaseOrderPdfModel
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDate = order.OrderDate,
            ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            Status = order.Status.ToString(),
            Currency = string.IsNullOrWhiteSpace(order.Currency) ? "LKR" : order.Currency,
            PaymentTerms = supplier?.PaymentTerms ?? "Standard Terms",
            Remarks = "Please deliver goods with accompanying delivery note quoting this Purchase Order number.",
            CompanyName = company?.Name ?? "Apex Manufacturing Ltd",
            CompanyCode = company?.Code,
            BranchName = branch?.Name ?? "Main Facility",
            CompanyAddress = branch != null ? $"{branch.Name}, Corporate Industrial Zone" : "Corporate Headquarters",
            CompanyPhone = "+94 11 234 5678",
            CompanyEmail = "procurement@apexerp.local",
            SupplierName = supplier?.SupplierName ?? "Vendor",
            SupplierCode = supplier?.SupplierCode,
            SupplierContactPerson = supplier?.ContactPerson,
            SupplierEmail = supplier?.Email,
            SupplierPhone = supplier?.Phone,
            SupplierAddress = string.Join(", ", new[] { supplier?.AddressLine1, supplier?.City, supplier?.Country }.Where(s => !string.IsNullOrWhiteSpace(s))),
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount
        };

        int lineNum = 1;
        foreach (var item in order.Items.Where(i => i.IsActive))
        {
            products.TryGetValue(item.ProductId, out var product);
            uoms.TryGetValue(item.UnitOfMeasureId ?? Guid.Empty, out var uomCode);

            model.Items.Add(new PurchaseOrderPdfLineItem
            {
                LineNumber = lineNum++,
                ProductCode = product?.ProductCode ?? "PRD",
                ProductName = product?.ProductName ?? "Item",
                UnitOfMeasure = uomCode ?? item.UnitOfMeasureCode ?? "EA",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxAmount = item.TaxAmount,
                LineTotal = item.LineTotal,
                Notes = item.Notes
            });
        }

        return GeneratePdf(model);
    }

    public byte[] GeneratePdf(PurchaseOrderPdfModel model)
    {
        var doc = new SimplePdfDocument();

        // We estimate lines per page to support multi-page orders cleanly
        // First page has header (height ~260pt), leaving ~460pt for lines.
        // Each line takes 22pt. So ~18 lines on page 1.
        // Subsequent pages can fit ~28 lines.
        var lineBatches = PaginateItems(model.Items);
        int totalPages = lineBatches.Count;

        for (int p = 0; p < totalPages; p++)
        {
            var page = doc.NewPage();
            bool isFirstPage = (p == 0);
            bool isLastPage = (p == totalPages - 1);
            int pageNumber = p + 1;

            double currentY = PageHeight - 40; // 801.89

            // 1. Accent top bar
            page.DrawRect(MarginLeft, currentY, ContentWidth, 3, 0.059, 0.424, 0.741, fill: true);
            currentY -= 20;

            if (isFirstPage)
            {
                // Full Document Header
                page.DrawText(model.CompanyName, MarginLeft, currentY, 18, bold: true, r: 0.059, g: 0.106, b: 0.176);
                page.DrawTextRight("PURCHASE ORDER", MarginRight, currentY, 18, bold: true, r: 0.059, g: 0.424, b: 0.741);
                currentY -= 14;

                page.DrawText("ApexERP Procurement Management", MarginLeft, currentY, 9, bold: false, r: 0.392, g: 0.455, b: 0.545);

                // Status Badge (APPROVED)
                string statusText = (model.Status ?? "APPROVED").ToUpperInvariant();
                double badgeWidth = 72;
                double badgeHeight = 16;
                double badgeX = MarginRight - badgeWidth;
                double badgeY = currentY - 4;
                page.DrawRect(badgeX, badgeY, badgeWidth, badgeHeight, 0.063, 0.486, 0.255, fill: true);
                page.DrawTextCenter(statusText, badgeX + (badgeWidth / 2), badgeY + 4, 8, bold: true, r: 1.0, g: 1.0, b: 1.0);
                currentY -= 28;

                // Two Information Cards (Vendor Details vs Order Details)
                double cardWidth = (ContentWidth - 14) / 2; // ~254.64
                double cardHeight = 90;
                double col1X = MarginLeft;
                double col2X = MarginLeft + cardWidth + 14;

                // Card 1: Vendor
                page.DrawRect(col1X, currentY - cardHeight, cardWidth, cardHeight, 0.973, 0.980, 0.988, fill: true);
                page.DrawRect(col1X, currentY - cardHeight, cardWidth, cardHeight, 0.886, 0.910, 0.941, fill: false);
                page.DrawText("VENDOR / SUPPLIER", col1X + 10, currentY - 14, 8, bold: true, r: 0.059, g: 0.424, b: 0.741);
                page.DrawText(Truncate(model.SupplierName, 32), col1X + 10, currentY - 28, 10, bold: true, r: 0.059, g: 0.090, b: 0.165);
                page.DrawText($"Code: {model.SupplierCode ?? "-"}", col1X + 10, currentY - 42, 8, bold: false, r: 0.278, g: 0.333, b: 0.416);
                page.DrawText($"Contact: {Truncate(model.SupplierContactPerson ?? "-", 30)}", col1X + 10, currentY - 54, 8, bold: false, r: 0.278, g: 0.333, b: 0.416);
                page.DrawText($"Email: {Truncate(model.SupplierEmail ?? "-", 32)}", col1X + 10, currentY - 66, 8, bold: false, r: 0.278, g: 0.333, b: 0.416);
                page.DrawText($"Phone: {model.SupplierPhone ?? "-"}", col1X + 10, currentY - 78, 8, bold: false, r: 0.278, g: 0.333, b: 0.416);

                // Card 2: Order Information
                page.DrawRect(col2X, currentY - cardHeight, cardWidth, cardHeight, 0.973, 0.980, 0.988, fill: true);
                page.DrawRect(col2X, currentY - cardHeight, cardWidth, cardHeight, 0.886, 0.910, 0.941, fill: false);
                page.DrawText("ORDER DETAILS", col2X + 10, currentY - 14, 8, bold: true, r: 0.059, g: 0.424, b: 0.741);
                page.DrawText($"PO Number:", col2X + 10, currentY - 28, 9, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(model.OrderNumber, col2X + cardWidth - 10, currentY - 28, 10, bold: true, r: 0.059, g: 0.090, b: 0.165);
                page.DrawText($"Order Date:", col2X + 10, currentY - 42, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(model.OrderDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), col2X + cardWidth - 10, currentY - 42, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);
                page.DrawText($"Expected Delivery:", col2X + 10, currentY - 54, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(model.ExpectedDeliveryDate?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "TBD", col2X + cardWidth - 10, currentY - 54, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);
                page.DrawText($"Currency:", col2X + 10, currentY - 66, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(model.Currency, col2X + cardWidth - 10, currentY - 66, 8, bold: true, r: 0.059, g: 0.090, b: 0.165);
                page.DrawText($"Payment Terms:", col2X + 10, currentY - 78, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(model.PaymentTerms ?? "Net 30", col2X + cardWidth - 10, currentY - 78, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);

                currentY -= (cardHeight + 20);
            }
            else
            {
                // Compact Header on Subsequent Pages
                page.DrawText($"{model.CompanyName} - Purchase Order #{model.OrderNumber}", MarginLeft, currentY, 11, bold: true, r: 0.059, g: 0.106, b: 0.176);
                page.DrawTextRight($"Page {pageNumber} of {totalPages}", MarginRight, currentY, 9, bold: false, r: 0.392, g: 0.455, b: 0.545);
                currentY -= 20;
            }

            // Line Items Table Header
            double tableHeaderHeight = 20;
            page.DrawRect(MarginLeft, currentY - tableHeaderHeight, ContentWidth, tableHeaderHeight, 0.945, 0.961, 0.976, fill: true);
            page.DrawRect(MarginLeft, currentY - tableHeaderHeight, ContentWidth, tableHeaderHeight, 0.886, 0.910, 0.941, fill: false);

            double colNo = MarginLeft + 6;
            double colItem = MarginLeft + 30;
            double colQty = MarginLeft + 270;
            double colUom = MarginLeft + 325;
            double colPrice = MarginLeft + 380;
            double colDisc = MarginLeft + 445;
            double colTotal = MarginRight - 8;

            page.DrawText("#", colNo, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);
            page.DrawText("ITEM & DESCRIPTION", colItem, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);
            page.DrawTextRight("QTY", colQty, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);
            page.DrawTextCenter("UOM", colUom, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);
            page.DrawTextRight("UNIT PRICE", colPrice, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);
            page.DrawTextRight("DISC", colDisc, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);
            page.DrawTextRight("TOTAL", colTotal, currentY - 14, 8, bold: true, r: 0.278, g: 0.333, b: 0.416);

            currentY -= tableHeaderHeight;

            // Render batch lines
            var currentLines = lineBatches[p];
            double rowHeight = 22;

            for (int i = 0; i < currentLines.Count; i++)
            {
                var line = currentLines[i];
                bool isEven = (i % 2 == 0);

                if (!isEven)
                {
                    page.DrawRect(MarginLeft, currentY - rowHeight, ContentWidth, rowHeight, 0.973, 0.980, 0.988, fill: true);
                }
                // Row bottom border
                page.DrawLine(MarginLeft, currentY - rowHeight, MarginRight, currentY - rowHeight, 0.925, 0.941, 0.961, 0.5);

                double textBaseline = currentY - 15;
                page.DrawText(line.LineNumber.ToString(), colNo, textBaseline, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                
                string itemLabel = $"{line.ProductCode} - {line.ProductName}";
                page.DrawText(Truncate(itemLabel, 44), colItem, textBaseline, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);
                page.DrawTextRight(line.Quantity.ToString("N2", CultureInfo.InvariantCulture), colQty, textBaseline, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);
                page.DrawTextCenter(line.UnitOfMeasure ?? "EA", colUom, textBaseline, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(line.UnitPrice.ToString("N2", CultureInfo.InvariantCulture), colPrice, textBaseline, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);
                page.DrawTextRight(line.DiscountAmount > 0 ? line.DiscountAmount.ToString("N2", CultureInfo.InvariantCulture) : "-", colDisc, textBaseline, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight(line.LineTotal.ToString("N2", CultureInfo.InvariantCulture), colTotal, textBaseline, 8, bold: true, r: 0.059, g: 0.090, b: 0.165);

                currentY -= rowHeight;
            }

            // If last page, render Summary & Signoff
            if (isLastPage)
            {
                currentY -= 15;

                // Summary Totals Card (Right side)
                double totalsWidth = 200;
                double totalsHeight = 85;
                double totalsX = MarginRight - totalsWidth;

                page.DrawRect(totalsX, currentY - totalsHeight, totalsWidth, totalsHeight, 0.973, 0.980, 0.988, fill: true);
                page.DrawRect(totalsX, currentY - totalsHeight, totalsWidth, totalsHeight, 0.886, 0.910, 0.941, fill: false);

                double totY = currentY - 15;
                page.DrawText("Subtotal:", totalsX + 12, totY, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight($"{model.Currency} {model.SubTotal:N2}", MarginRight - 12, totY, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);

                totY -= 15;
                page.DrawText("Discount:", totalsX + 12, totY, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight($"- {model.Currency} {model.DiscountAmount:N2}", MarginRight - 12, totY, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);

                totY -= 15;
                page.DrawText("Tax / VAT:", totalsX + 12, totY, 8, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawTextRight($"{model.Currency} {model.TaxAmount:N2}", MarginRight - 12, totY, 8, bold: false, r: 0.059, g: 0.090, b: 0.165);

                // Highlighted Grand Total Bar
                totY -= 22;
                page.DrawRect(totalsX, totY - 4, totalsWidth, 24, 0.945, 0.961, 0.976, fill: true);
                page.DrawLine(totalsX, totY + 20, MarginRight, totY + 20, 0.886, 0.910, 0.941, 1);
                page.DrawText("TOTAL AMOUNT:", totalsX + 12, totY + 4, 9, bold: true, r: 0.059, g: 0.106, b: 0.176);
                page.DrawTextRight($"{model.Currency} {model.TotalAmount:N2}", MarginRight - 12, totY + 4, 11, bold: true, r: 0.059, g: 0.424, b: 0.741);

                // Remarks & Instructions (Left side)
                double remarksWidth = ContentWidth - totalsWidth - 20;
                page.DrawText("SPECIAL INSTRUCTIONS & REMARKS", MarginLeft, currentY - 12, 8, bold: true, r: 0.059, g: 0.424, b: 0.741);
                page.DrawText(Truncate(model.Remarks ?? "Standard delivery and packing terms apply.", 60), MarginLeft, currentY - 26, 8, bold: false, r: 0.278, g: 0.333, b: 0.416);
                page.DrawText("1. Invoice must quote Purchase Order number for payment verification.", MarginLeft, currentY - 40, 7.5, bold: false, r: 0.392, g: 0.455, b: 0.545);
                page.DrawText("2. Goods subject to warehouse inspection and count at receiving dock.", MarginLeft, currentY - 52, 7.5, bold: false, r: 0.392, g: 0.455, b: 0.545);

                currentY -= (totalsHeight + 25);

                // Sign-off section
                double signBoxWidth = 140;
                page.DrawLine(MarginLeft, currentY, MarginLeft + signBoxWidth, currentY, 0.6, 0.6, 0.6, 0.8);
                page.DrawText("Authorized Procurement Signatory", MarginLeft, currentY - 12, 7.5, bold: false, r: 0.392, g: 0.455, b: 0.545);

                double signBox2X = MarginLeft + signBoxWidth + 40;
                page.DrawLine(signBox2X, currentY, signBox2X + signBoxWidth, currentY, 0.6, 0.6, 0.6, 0.8);
                page.DrawText("Supplier Acceptance & Date", signBox2X, currentY - 12, 7.5, bold: false, r: 0.392, g: 0.455, b: 0.545);
            }

            // Universal Page Footer
            double footerY = 28;
            page.DrawLine(MarginLeft, footerY + 12, MarginRight, footerY + 12, 0.886, 0.910, 0.941, 0.5);
            page.DrawText("Generated automatically via ApexERP Procurement Operations &bull; Official Business Document", MarginLeft, footerY, 7.5, bold: false, r: 0.6, 0.6, 0.6);
            page.DrawTextRight($"Page {pageNumber} of {totalPages}", MarginRight, footerY, 7.5, bold: false, r: 0.6, 0.6, 0.6);
        }

        return doc.Save();
    }

    private static List<List<PurchaseOrderPdfLineItem>> PaginateItems(List<PurchaseOrderPdfLineItem> items)
    {
        var result = new List<List<PurchaseOrderPdfLineItem>>();
        if (items.Count == 0)
        {
            result.Add(new List<PurchaseOrderPdfLineItem>());
            return result;
        }

        // Page 1 has header and cards: fits up to 14 items if last page (to allow summary box),
        // or up to 18 items if multi-page.
        int firstPageCapacity = items.Count <= 14 ? 14 : 18;
        int otherPageCapacity = 26;

        int index = 0;
        int remaining = items.Count;

        // Page 1
        int count1 = Math.Min(firstPageCapacity, remaining);
        result.Add(items.GetRange(index, count1));
        index += count1;
        remaining -= count1;

        // Subsequent pages
        while (remaining > 0)
        {
            // If this is the last page, reserve space for totals
            int capacity = (remaining <= 20) ? 20 : otherPageCapacity;
            int count = Math.Min(capacity, remaining);
            result.Add(items.GetRange(index, count));
            index += count;
            remaining -= count;
        }

        return result;
    }

    private static string Truncate(string val, int maxLen)
    {
        if (string.IsNullOrEmpty(val) || val.Length <= maxLen) return val;
        return val.Substring(0, maxLen - 2) + "..";
    }
}

/// <summary>
/// Minimal, high-performance ISO 32000-1 (PDF 1.4) in-memory builder.
/// Generates cross-reference tables, stream lengths, and standard Type 1 font references.
/// </summary>
internal class SimplePdfDocument
{
    private readonly List<SimplePdfPage> _pages = new();

    public SimplePdfPage NewPage()
    {
        var page = new SimplePdfPage();
        _pages.Add(page);
        return page;
    }

    public byte[] Save()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        // Header
        writer.Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        writer.Flush();

        var offsets = new List<long>();
        int objCount = 0;

        // Helper to register object offset
        void StartObject(int id)
        {
            while (offsets.Count < id)
                offsets.Add(0);
            writer.Flush();
            offsets[id - 1] = ms.Position;
            writer.Write($"{id} 0 obj\n");
        }

        // Object 1: Catalog
        int catalogId = 1;
        int pagesId = 2;
        int fontHelveticaId = 3;
        int fontHelveticaBoldId = 4;

        StartObject(catalogId);
        writer.Write($"<< /Type /Catalog /Pages {pagesId} 0 R >>\nendobj\n");

        // Object 3: Font Helvetica (Regular)
        StartObject(fontHelveticaId);
        writer.Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Object 4: Font Helvetica-Bold
        StartObject(fontHelveticaBoldId);
        writer.Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        // Allocate IDs for pages and content streams
        int nextId = 5;
        var pageIds = new List<int>();
        var contentIds = new List<int>();

        for (int i = 0; i < _pages.Count; i++)
        {
            pageIds.Add(nextId++);
            contentIds.Add(nextId++);
        }

        // Object 2: Pages Tree
        StartObject(pagesId);
        string kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
        writer.Write($"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>\nendobj\n");

        // Page Objects & Content Streams
        for (int i = 0; i < _pages.Count; i++)
        {
            int pId = pageIds[i];
            int cId = contentIds[i];
            var page = _pages[i];

            // Page Object
            StartObject(pId);
            writer.Write($"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 595.28 841.89] /Contents {cId} 0 R /Resources << /Font << /F1 {fontHelveticaId} 0 R /F2 {fontHelveticaBoldId} 0 R >> >> >>\nendobj\n");

            // Content Stream Object
            byte[] contentBytes = page.GetContentBytes();
            StartObject(cId);
            writer.Write($"<< /Length {contentBytes.Length} >>\nstream\n");
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            writer.Write("\nendstream\nendobj\n");
        }

        writer.Flush();
        long xrefOffset = ms.Position;
        objCount = nextId - 1;

        // XRef Table
        writer.Write("xref\n");
        writer.Write($"0 {objCount + 1}\n");
        writer.Write("0000000000 65535 f \n");
        for (int i = 0; i < objCount; i++)
        {
            long offset = offsets[i];
            writer.Write($"{offset:D10} 00000 n \n");
        }

        // Trailer
        writer.Write("trailer\n");
        writer.Write($"<< /Size {objCount + 1} /Root {catalogId} 0 R >>\n");
        writer.Write("startxref\n");
        writer.Write($"{xrefOffset}\n");
        writer.Write("%%EOF\n");
        writer.Flush();

        return ms.ToArray();
    }
}

internal class SimplePdfPage
{
    private readonly StringBuilder _sb = new();

    public void DrawRect(double x, double y, double width, double height, double r, double g, double b, bool fill)
    {
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} ", r, g, b);
        if (fill)
        {
            _sb.Append("rg\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re f\n", x, y, width, height);
        }
        else
        {
            _sb.Append("RG\n0.5 w\n");
            _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2} {3:F2} re S\n", x, y, width, height);
        }
    }

    public void DrawLine(double x1, double y1, double x2, double y2, double r, double g, double b, double lineWidth = 1.0)
    {
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} RG\n", r, g, b);
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} w\n", lineWidth);
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} m {2:F2} {3:F2} l S\n", x1, y1, x2, y2);
    }

    public void DrawText(string text, double x, double y, double fontSize, bool bold = false, double r = 0, double g = 0, double b = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        string escaped = EscapePdfText(text);
        string font = bold ? "/F2" : "/F1";

        _sb.Append("BT\n");
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1:F1} Tf\n", font, fontSize);
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} {1:F3} {2:F3} rg\n", r, g, b);
        _sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} {1:F2} Td\n", x, y);
        _sb.Append($"({escaped}) Tj\n");
        _sb.Append("ET\n");
    }

    public void DrawTextRight(string text, double rightX, double y, double fontSize, bool bold = false, double r = 0, double g = 0, double b = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        double approxWidth = EstimateTextWidth(text, fontSize, bold);
        double x = rightX - approxWidth;
        DrawText(text, x, y, fontSize, bold, r, g, b);
    }

    public void DrawTextCenter(string text, double centerX, double y, double fontSize, bool bold = false, double r = 0, double g = 0, double b = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        double approxWidth = EstimateTextWidth(text, fontSize, bold);
        double x = centerX - (approxWidth / 2);
        DrawText(text, x, y, fontSize, bold, r, g, b);
    }

    private static double EstimateTextWidth(string text, double fontSize, bool bold)
    {
        // Average char width factor for Helvetica standard font ~0.52 to 0.58
        double factor = bold ? 0.56 : 0.52;
        return text.Length * fontSize * factor;
    }

    private static string EscapePdfText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("&bull;", " - ");
    }

    public byte[] GetContentBytes()
    {
        return Encoding.ASCII.GetBytes(_sb.ToString());
    }
}
