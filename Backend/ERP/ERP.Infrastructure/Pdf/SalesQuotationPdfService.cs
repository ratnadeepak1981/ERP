using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Sales.SalesQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Pdf;

/// <summary>
/// Standards-compliant ISO 32000-1 (PDF 1.4) generator for Sales Quotations.
/// Operates with pure .NET BCL (zero external NuGet dependencies), produces valid PDF binary streams.
/// </summary>
public class SalesQuotationPdfService : ISalesQuotationPdfService
{
    private readonly DomainDbContext _dbContext;
    private readonly ILogger<SalesQuotationPdfService> _logger;

    public SalesQuotationPdfService(DomainDbContext dbContext, ILogger<SalesQuotationPdfService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> GeneratePdfForQuotationAsync(
        Guid tenantId,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.SalesQuotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quotationId && q.TenantId == tenantId, cancellationToken);

        if (quotation == null)
            throw new KeyNotFoundException($"Sales quotation '{quotationId}' was not found.");

        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(c => c.Id == quotation.CompanyId && c.TenantId == tenantId, cancellationToken);

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == quotation.CustomerId && c.TenantId == tenantId, cancellationToken);

        var productIds = quotation.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.ProductName, cancellationToken);

        return BuildPdf(quotation, company?.Name ?? "ApexERP Company", customer?.CustomerName ?? "Customer", products);
    }

    private static byte[] BuildPdf(
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quote,
        string companyName,
        string customerName,
        Dictionary<Guid, string> products)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 20 Tf");
        content.AppendLine("50 780 Td");
        content.AppendLine($"({EscapePdf(companyName)}) Tj");
        content.AppendLine("ET");

        content.AppendLine("BT");
        content.AppendLine("/F1 16 Tf");
        content.AppendLine("50 750 Td");
        content.AppendLine($"(FORMAL SALES QUOTATION - {EscapePdf(quote.QuotationNumber)}) Tj");
        content.AppendLine("ET");

        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("50 720 Td");
        content.AppendLine($"({EscapePdf($"Customer: {customerName}")}) Tj");
        content.AppendLine("0 -15 Td");
        content.AppendLine($"({EscapePdf($"Date: {quote.QuotationDate:yyyy-MM-dd}   |   Valid Until: {quote.ValidUntilDateUtc:yyyy-MM-dd} (23:59:59 UTC)")}) Tj");
        content.AppendLine("0 -15 Td");
        content.AppendLine($"({EscapePdf($"Currency: {quote.Currency}   |   Payment Terms: {quote.PaymentTerms ?? "Net 30 Days"}")}) Tj");
        content.AppendLine("0 -15 Td");
        content.AppendLine($"({EscapePdf($"Status: {quote.Status}")}) Tj");
        content.AppendLine("ET");

        // Table header
        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("50 640 Td");
        content.AppendLine($"(ITEM DESCRIPTION                      QTY       UNIT PRICE     DISC %      TOTAL ({quote.Currency})) Tj");
        content.AppendLine("ET");

        double y = 620;
        foreach (var item in quote.Items.Where(i => i.IsActive))
        {
            products.TryGetValue(item.ProductId, out var name);
            name ??= "Product " + item.ProductId.ToString()[..8];
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0,-35} {1,6:F2} {2,14:F2} {3,10:F2} {4,16:F2}",
                name.Length > 32 ? name[..32] : name,
                item.Quantity,
                item.UnitPrice,
                item.DiscountRate,
                item.LineTotal);

            content.AppendLine("BT");
            content.AppendLine("/F1 9 Tf");
            content.AppendLine($"50 {y:F1} Td");
            content.AppendLine($"({EscapePdf(line)}) Tj");
            content.AppendLine("ET");
            y -= 15;
            if (y < 120) break;
        }

        // Totals
        content.AppendLine("BT");
        content.AppendLine("/F1 11 Tf");
        content.AppendLine($"300 {y - 20:F1} Td");
        content.AppendLine($"({EscapePdf($"Sub Total:       {quote.SubTotal:F2} {quote.Currency}")}) Tj");
        content.AppendLine("0 -15 Td");
        content.AppendLine($"({EscapePdf($"Total Discount:  {quote.DiscountAmount:F2} {quote.Currency}")}) Tj");
        content.AppendLine("0 -15 Td");
        content.AppendLine($"({EscapePdf($"Tax:             {quote.TaxAmount:F2} {quote.Currency}")}) Tj");
        content.AppendLine("0 -18 Td");
        content.AppendLine($"({EscapePdf($"GRAND TOTAL:     {quote.TotalAmount:F2} {quote.Currency}")}) Tj");
        content.AppendLine("ET");

        string contentStream = content.ToString();
        byte[] contentBytes = Encoding.ASCII.GetBytes(contentStream);

        // Simple valid PDF structure
        writer.Write("%PDF-1.4\n");
        writer.Flush();

        long obj1 = ms.Position;
        writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        writer.Flush();

        long obj2 = ms.Position;
        writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        writer.Flush();

        long obj3 = ms.Position;
        writer.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n");
        writer.Flush();

        long obj4 = ms.Position;
        writer.Write($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        writer.Flush();
        ms.Write(contentBytes, 0, contentBytes.Length);
        writer.Write("\nendstream\nendobj\n");
        writer.Flush();

        long obj5 = ms.Position;
        writer.Write("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        writer.Flush();

        long xref = ms.Position;
        writer.Write("xref\n0 6\n0000000000 65535 f \n");
        writer.Write($"{obj1:D10} 00000 n \n");
        writer.Write($"{obj2:D10} 00000 n \n");
        writer.Write($"{obj3:D10} 00000 n \n");
        writer.Write($"{obj4:D10} 00000 n \n");
        writer.Write($"{obj5:D10} 00000 n \n");
        writer.Write("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n");
        writer.Write($"{xref}\n%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdf(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
