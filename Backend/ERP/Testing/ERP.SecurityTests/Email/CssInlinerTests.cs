using ERP.Infrastructure.Email.Templates;
using Xunit;

namespace ERP.SecurityTests.Email;

public class CssInlinerTests
{
    [Fact]
    public void InlineCss_ClassSelector_AppliesStylesToMatchingElements()
    {
        var html = @"<div class=""header-box"">Welcome</div>";
        var css = @".header-box { background-color: #0f6cbd; color: #ffffff; }";

        var result = CssInliner.InlineCss(html, css);

        Assert.Contains(@"style=""background-color: #0f6cbd; color: #ffffff;""", result);
    }

    [Fact]
    public void InlineCss_ElementSelector_AppliesStylesToTag()
    {
        var html = @"<p>This is a paragraph</p>";
        var css = @"p { font-size: 14px; line-height: 20px; }";

        var result = CssInliner.InlineCss(html, css);

        Assert.Contains(@"style=""font-size: 14px; line-height: 20px;""", result);
    }

    [Fact]
    public void InlineCss_ExistingStyleAttribute_MergesCleanlyWithoutOverwriting()
    {
        var html = @"<div class=""card"" style=""border: 1px solid red;"">Content</div>";
        var css = @".card { background-color: #ffffff; }";

        var result = CssInliner.InlineCss(html, css);

        Assert.Contains("background-color: #ffffff;", result);
        Assert.Contains("border: 1px solid red;", result);
    }

    [Fact]
    public void InlineCss_MediaQueries_ArePreservedInHeadStyleBlock()
    {
        var html = @"<html><head></head><body><div class=""test"">Hi</div></body></html>";
        var css = @"
            .test { color: black; }
            @media only screen and (max-width: 600px) {
                .test { color: blue; }
            }";

        var result = CssInliner.InlineCss(html, css);

        Assert.Contains("<style>", result);
        Assert.Contains("@media only screen and (max-width: 600px)", result);
        Assert.Contains(@"style=""color: black;""", result);
    }
}
