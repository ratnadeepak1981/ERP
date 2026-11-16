using System;
using System.IO;
using System.Threading.Tasks;
using ERP.Application.Common.Email;
using ERP.Application.Features.Procurement.PurchaseOrderFeature;
using ERP.Application.Features.Procurement.PurchaseOrderFeature.Templates;
using ERP.Infrastructure.Email;
using ERP.Infrastructure.Email.Configuration;
using ERP.Infrastructure.Email.Templates;
using ERP.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.SecurityTests.Email;

public class SmtpLiveDeliveryTests
{
    [Fact]
    public async Task SendLiveEmail_ToConfiguredSupplier_SucceedsWithAttachment()
    {
        // 1. Load appsettings.json from API project
        var apiPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Backend", "ERP", "API");
        if (!Directory.Exists(apiPath))
        {
            apiPath = @"Z:\ERP\Backend\ERP\API";
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiPath) ? apiPath : AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var smtpSection = config.GetSection(SmtpOptions.SectionName);
        var smtpOptions = new SmtpOptions();
        smtpSection.Bind(smtpOptions);

        Assert.False(string.IsNullOrWhiteSpace(smtpOptions.Host), "SMTP Host must be configured.");
        Assert.False(string.IsNullOrWhiteSpace(smtpOptions.Username), "SMTP Username must be configured.");
        Assert.False(string.IsNullOrWhiteSpace(smtpOptions.Password), "SMTP Password must be configured.");

        // 2. Resolve template engine
        var templateEngine = new EmailTemplateEngine(NullLogger<EmailTemplateEngine>.Instance);

        var emailModel = new PurchaseOrderEmailModel
        {
            CompanyName = "Apxtest Tenant (Pvt) Ltd",
            SupplierName = "Lanka Retail Distributors (Pvt) Ltd",
            OrderNumber = "PO-2026-6506",
            OrderDate = "16 Nov 2026",
            ExpectedDeliveryDate = "25 Nov 2026",
            Currency = "LKR",
            TotalAmount = "150,000.00",
            Remarks = "Approved PO supplier email verification test.",
            ContactPerson = "Procurement Department",
            CompanyEmail = "ratnadeepak1981@gmail.com",
            CompanyPhone = "+94 11 234 5678",
            CurrentYear = DateTime.UtcNow.Year.ToString()
        };

        var htmlBody = await templateEngine.RenderAsync("PurchaseOrder/PurchaseOrderEmail", emailModel);
        Assert.NotNull(htmlBody);
        Assert.Contains("PO-2026-6506", htmlBody);

        // 3. Generate PDF attachment using Pure C# ISO 32000-1 generator
        var pdfService = new PurchaseOrderPdfService(null!, NullLogger<PurchaseOrderPdfService>.Instance);
        var pdfModel = new PurchaseOrderPdfModel
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "PO-2026-6506",
            OrderDate = DateTime.UtcNow,
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7),
            Status = "Approved",
            Currency = "LKR",
            PaymentTerms = "Net 30 Days",
            CompanyName = "Apxtest Tenant (Pvt) Ltd",
            CompanyCode = "APXT",
            BranchName = "Apxtest Tenant Main Branch",
            SupplierName = "Lanka Retail Distributors (Pvt) Ltd",
            SupplierCode = "SUP-RET-001",
            SupplierEmail = "ratnadeepak1981@gmail.com",
            SupplierPhone = "+94 11 234 5678",
            Remarks = "Official Purchase Order dispatched via ApexERP email service.",
            SubTotal = 150000.00m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 150000.00m
        };

        pdfModel.Items.Add(new PurchaseOrderPdfLineItem
        {
            LineNumber = 1,
            ProductCode = "RET-001",
            ProductName = "Samba Rice 5kg Pack",
            UnitOfMeasure = "BAG",
            Quantity = 100,
            UnitPrice = 1000.00m,
            LineTotal = 100000.00m
        });

        pdfModel.Items.Add(new PurchaseOrderPdfLineItem
        {
            LineNumber = 2,
            ProductCode = "RET-002",
            ProductName = "Pure Vegetable Cooking Oil 1L",
            UnitOfMeasure = "BTL",
            Quantity = 50,
            UnitPrice = 1000.00m,
            LineTotal = 50000.00m
        });

        var pdfBytes = pdfService.GeneratePdf(pdfModel);
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);

        // 4. Construct EmailMessage
        var emailMessage = new EmailMessage
        {
            Subject = $"Purchase Order #PO-2026-6506 - Apxtest Tenant (Pvt) Ltd",
            HtmlBody = htmlBody,
            PlainTextBody = $"Dear Lanka Retail Distributors (Pvt) Ltd,\n\nPlease find attached official Purchase Order #PO-2026-6506.\n\nTotal Amount: LKR 150,000.00\n\nApexERP System"
        };

        emailMessage.To.Add(new EmailRecipient("ratnadeepak1981@gmail.com", "Lanka Retail Distributors (Pvt) Ltd"));
        emailMessage.Attachments.Add(new EmailAttachment("PO-2026-6506.pdf", "application/pdf", pdfBytes));

        // 5. Send using production MailKitSmtpEmailService
        var emailService = new MailKitSmtpEmailService(
            Options.Create(smtpOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            new DefaultSmtpClientFactory());

        var result = await emailService.SendEmailAsync(emailMessage);

        Assert.True(result.IsSuccess, $"Email delivery failed: {result.ErrorMessage}");
        Assert.False(string.IsNullOrWhiteSpace(result.MessageId));
    }
}
