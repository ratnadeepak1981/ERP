using System.IO;
using System.Threading.Tasks;
using ERP.Application.Features.Procurement.PurchaseOrderFeature.Templates;
using ERP.Infrastructure.Email.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SecurityTests.Email;

public class PurchaseOrderTemplatePreviewTests
{
    [Fact]
    public async Task GeneratePurchaseOrderEmailPreview_CreatesFullSampleHtmlFile()
    {
        var templateRoot = @"Z:\ERP\Backend\Views\Templates";
        var engine = new EmailTemplateEngine(NullLogger<EmailTemplateEngine>.Instance, templateRoot);

        var sampleModel = new PurchaseOrderEmailModel
        {
            CompanyName = "Lanka Organic Agro Exports (Pvt) Ltd",
            SupplierName = "Highland Agro Packaging Industries",
            OrderNumber = "PO-2026-11-0089",
            OrderDate = "16 Nov 2026",
            ExpectedDeliveryDate = "25 Nov 2026",
            Currency = "LKR",
            TotalAmount = "2,450,000.00",
            Remarks = "Fragile packaging required. Delivery to Central Retail Warehouse LOC-REC-01 before 2:00 PM.",
            ContactPerson = "Chaminda Perera (Head of Procurement)",
            CompanyEmail = "procurement@lankaagro.local",
            CompanyPhone = "+94 11 234 5678",
            CurrentYear = "2026"
        };

        var renderedHtml = await engine.RenderAsync("PurchaseOrder/PurchaseOrderEmail", sampleModel);

        Assert.NotNull(renderedHtml);
        Assert.NotEmpty(renderedHtml);

        // Save preview file so it can be visually inspected without sending an email
        var previewPath = Path.Combine(templateRoot, "PurchaseOrder", "PurchaseOrderEmail.preview.html");
        await File.WriteAllTextAsync(previewPath, renderedHtml);

        Assert.True(File.Exists(previewPath));
        Assert.Contains("Lanka Organic Agro Exports (Pvt) Ltd", renderedHtml);
        Assert.Contains("Highland Agro Packaging Industries", renderedHtml);
        Assert.Contains("PO-2026-11-0089", renderedHtml);
        Assert.Contains("2,450,000.00", renderedHtml);
        Assert.Contains("style=\"background-color: #0f6cbd;", renderedHtml);
        Assert.Contains("style=\"background-color: #107c41;", renderedHtml);
    }
}
