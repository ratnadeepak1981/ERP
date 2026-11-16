using System;
using ERP.Application.Common.Email;
using Xunit;

namespace ERP.SecurityTests.Email;

public class EmailMessageValidationTests
{
    [Fact]
    public void EmailRecipient_ValidAddress_InitializesCorrectly()
    {
        var recipient = new EmailRecipient("supplier@example.com", "Acme Corp");

        Assert.Equal("supplier@example.com", recipient.Address);
        Assert.Equal("Acme Corp", recipient.DisplayName);
        Assert.Equal("Acme Corp <supplier@example.com>", recipient.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@missingusername.com")]
    public void EmailRecipient_InvalidAddress_ThrowsArgumentException(string invalidAddress)
    {
        Assert.Throws<ArgumentException>(() => new EmailRecipient(invalidAddress));
    }

    [Fact]
    public void EmailMessage_Validate_ThrowsWhenNoRecipients()
    {
        var message = new EmailMessage
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Test</p>"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => message.Validate());
        Assert.Contains("At least one recipient", ex.Message);
    }

    [Fact]
    public void EmailMessage_Validate_ThrowsWhenEmptySubject()
    {
        var message = new EmailMessage
        {
            HtmlBody = "<p>Test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var ex = Assert.Throws<InvalidOperationException>(() => message.Validate());
        Assert.Contains("subject cannot be empty", ex.Message);
    }

    [Fact]
    public void EmailMessage_Validate_ThrowsWhenBothHtmlAndPlainTextEmpty()
    {
        var message = new EmailMessage
        {
            Subject = "Test Subject",
            HtmlBody = "",
            PlainTextBody = null
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var ex = Assert.Throws<InvalidOperationException>(() => message.Validate());
        Assert.Contains("must have either an HTML body or a plain-text body", ex.Message);
    }

    [Fact]
    public void EmailMessage_Validate_SucceedsForValidMessage()
    {
        var message = new EmailMessage
        {
            Subject = "Purchase Order PO-001",
            HtmlBody = "<p>PO Attached</p>",
            PlainTextBody = "PO Attached"
        };
        message.To.Add(new EmailRecipient("supplier@example.com", "Supplier A"));
        message.Cc.Add(new EmailRecipient("procurement@company.local"));
        message.Bcc.Add(new EmailRecipient("audit@company.local"));
        message.Attachments.Add(new EmailAttachment("PO_001.pdf", "application/pdf", new byte[] { 1, 2, 3 }));

        // Should not throw
        message.Validate();
        Assert.Single(message.To);
        Assert.Single(message.Cc);
        Assert.Single(message.Bcc);
        Assert.Single(message.Attachments);
    }
}
