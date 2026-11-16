using System;
using ERP.Infrastructure.Email.Configuration;
using Xunit;

namespace ERP.SecurityTests.Email;

public class SmtpOptionsValidationTests
{
    [Fact]
    public void SmtpOptions_DefaultSettings_AreConfiguredForGmail()
    {
        var options = new SmtpOptions();

        Assert.Equal("smtp.gmail.com", options.Host);
        Assert.Equal(587, options.Port);
        Assert.True(options.EnableSsl);
    }

    [Fact]
    public void SmtpOptions_Validate_FailsWhenHostMissing()
    {
        var options = new SmtpOptions
        {
            Host = "",
            Port = 587,
            SenderEmail = "admin@example.com"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Host", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void SmtpOptions_Validate_FailsWhenPortInvalid(int port)
    {
        var options = new SmtpOptions
        {
            Host = "smtp.gmail.com",
            Port = port,
            SenderEmail = "admin@example.com"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Port", ex.Message);
    }

    [Fact]
    public void SmtpOptions_Validate_FailsWhenSenderEmailMissing()
    {
        var options = new SmtpOptions
        {
            Host = "smtp.gmail.com",
            Port = 587,
            SenderEmail = "   "
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("SenderEmail", ex.Message);
    }

    [Fact]
    public void SmtpOptions_Validate_SucceedsForValidGmailConfiguration()
    {
        var options = new SmtpOptions
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            SenderEmail = "procurement@company.com",
            SenderDisplayName = "ApexERP Procurement",
            Username = "procurement@gmail.com",
            Password = "abcd efgh ijkl mnop"
        };

        // Should not throw
        options.Validate();
    }
}
