using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ERP.Application.Features.Procurement.PurchaseOrderFeature.Templates;
using ERP.Infrastructure.Email.Templates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.SecurityTests.Email;

public class EmailTemplateEngineTests
{
    private readonly EmailTemplateEngine _engine;

    public EmailTemplateEngineTests()
    {
        // Standard template folder root: Z:\ERP\Backend\Views\Templates
        _engine = new EmailTemplateEngine(NullLogger<EmailTemplateEngine>.Instance, @"Z:\ERP\Backend\Views\Templates");
    }

    [Fact]
    public async Task RenderAsync_PurchaseOrderTemplate_RendersWithModelPropertiesAndInlinedCss()
    {
        var model = new PurchaseOrderEmailModel
        {
            CompanyName = "Lanka Agro Exports (Pvt) Ltd",
            SupplierName = "Green Packaging Solutions",
            OrderNumber = "PO-2026-11-0042",
            OrderDate = "2026-11-16",
            ExpectedDeliveryDate = "2026-11-25",
            Currency = "LKR",
            TotalAmount = "1,250,000.00",
            Remarks = "Standard handling. Deliver to Bay 3.",
            ContactPerson = "Sunil Weerakkody",
            CompanyEmail = "procurement@lankaagro.local",
            CompanyPhone = "+94 11 234 5678",
            CurrentYear = "2026"
        };

        var renderedHtml = await _engine.RenderAsync("PurchaseOrder/PurchaseOrderEmail", model);

        // Verify placeholder replacements
        Assert.Contains("Lanka Agro Exports (Pvt) Ltd", renderedHtml);
        Assert.Contains("Green Packaging Solutions", renderedHtml);
        Assert.Contains("PO-2026-11-0042", renderedHtml);
        Assert.Contains("1,250,000.00", renderedHtml);
        Assert.Contains("Standard handling. Deliver to Bay 3.", renderedHtml);
        Assert.Contains("Sunil Weerakkody", renderedHtml);

        // Verify CSS Inlining has populated style tags on classes
        Assert.Contains(@"style=""background-color: #0f6cbd;", renderedHtml);
        Assert.Contains(@"style=""color: #0f172a;", renderedHtml);
        Assert.Contains("APPROVED", renderedHtml);
    }

    [Fact]
    public async Task RenderAsync_SpecialCharacters_AreHtmlEncodedToPreventInjection()
    {
        var placeholders = new Dictionary<string, string?>
        {
            ["CompanyName"] = "Smith & Wesson <Industrial>",
            ["OrderNumber"] = "PO-9999",
            ["SupplierName"] = "XYZ Co"
        };

        var renderedHtml = await _engine.RenderAsync("PurchaseOrder/PurchaseOrderEmail", placeholders);

        // Verifies ampersands and angle brackets are encoded
        Assert.Contains("Smith &amp; Wesson &lt;Industrial&gt;", renderedHtml);
        Assert.DoesNotContain("<Industrial>", renderedHtml);
    }

    [Fact]
    public async Task RenderAsync_NonExistentTemplate_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _engine.RenderAsync("NonExistentModule/NonExistentEmail", new Dictionary<string, string?>()));
    }

    [Fact]
    public async Task RenderAsync_WithDefaultConstructor_Succeeds()
    {
        var engine = new EmailTemplateEngine(NullLogger<EmailTemplateEngine>.Instance);
        var model = new PurchaseOrderEmailModel
        {
            CompanyName = "Apxtest Tenant (Pvt) Ltd",
            SupplierName = "Lanka Retail Distributors (Pvt) Ltd",
            OrderNumber = "PO-2026-6506",
            OrderDate = "16 Nov 2026",
            ExpectedDeliveryDate = "25 Nov 2026",
            Currency = "LKR",
            TotalAmount = "150,000.00",
            Remarks = "Approved PO test",
            ContactPerson = "Procurement Dept",
            CompanyEmail = "ratnadeepak1981@gmail.com",
            CompanyPhone = "+94 11 234 5678",
            CurrentYear = "2026"
        };
        var html = await engine.RenderAsync("PurchaseOrder/PurchaseOrderEmail", model);
        Assert.NotNull(html);
        Assert.Contains("PO-2026-6506", html);
    }
}
