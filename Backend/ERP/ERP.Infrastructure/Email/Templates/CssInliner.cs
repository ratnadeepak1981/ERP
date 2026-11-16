using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ERP.Infrastructure.Email.Templates;

/// <summary>
/// Lightweight, dependency-free CSS inliner designed specifically for HTML emails.
/// Parses CSS rule sets and inlines style declarations directly into matching HTML tag style attributes.
/// </summary>
public static class CssInliner
{
    private static readonly Regex CommentRegex = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex RuleRegex = new(@"([^{]+)\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<([a-zA-Z0-9]+)([^>]*)>", RegexOptions.Compiled);
    private static readonly Regex ClassAttrRegex = new(@"class=([""'])(.*?)\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleAttrRegex = new(@"style=([""'])(.*?)\1", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public class CssRule
    {
        public string Selector { get; set; } = string.Empty;
        public string Declarations { get; set; } = string.Empty;
    }

    /// <summary>
    /// Inlines CSS declarations into HTML element style attributes.
    /// Also embeds a cleaned &lt;style&gt; block in the &lt;head&gt; for clients that support media queries.
    /// </summary>
    public static string InlineCss(string html, string css)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(css))
            return html;

        var rules = ParseCss(css);
        if (rules.Count == 0)
            return html;

        // Process HTML by replacing tags with inlined style attributes
        var modifiedHtml = HtmlTagRegex.Replace(html, match =>
        {
            var tagName = match.Groups[1].Value.ToLowerInvariant();
            var attributes = match.Groups[2].Value;

            // Extract classes if present
            var classMatch = ClassAttrRegex.Match(attributes);
            var classes = classMatch.Success
                ? classMatch.Groups[2].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();

            // Collect applicable styles for this element
            var matchedStyles = new List<string>();

            foreach (var rule in rules)
            {
                var selector = rule.Selector.Trim();

                // 1. Tag selector: "table", "p", "th", "td", "h1", "h2"
                if (string.Equals(selector, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    matchedStyles.Add(rule.Declarations);
                }
                // 2. Class selector: ".my-class"
                else if (selector.StartsWith(".") && classes.Contains(selector[1..], StringComparer.OrdinalIgnoreCase))
                {
                    matchedStyles.Add(rule.Declarations);
                }
                // 3. Tag.Class selector: "table.my-class"
                else if (selector.Contains('.'))
                {
                    var parts = selector.Split('.');
                    if (parts.Length == 2 &&
                        string.Equals(parts[0], tagName, StringComparison.OrdinalIgnoreCase) &&
                        classes.Contains(parts[1], StringComparer.OrdinalIgnoreCase))
                    {
                        matchedStyles.Add(rule.Declarations);
                    }
                }
            }

            if (matchedStyles.Count == 0)
                return match.Value;

            // Merge declarations
            var combinedNewStyles = string.Join(" ", matchedStyles).Trim();

            // Check if element already has style attribute
            var styleMatch = StyleAttrRegex.Match(attributes);
            string newAttributes;

            if (styleMatch.Success)
            {
                var existingStyle = styleMatch.Groups[2].Value.Trim();
                if (!existingStyle.EndsWith(";"))
                    existingStyle += ";";

                var mergedStyle = $"{combinedNewStyles} {existingStyle}".Trim();
                newAttributes = StyleAttrRegex.Replace(attributes, $"style=\"{mergedStyle}\"");
            }
            else
            {
                newAttributes = $"{attributes} style=\"{combinedNewStyles}\"";
            }

            return $"<{tagName}{newAttributes}>";
        });

        // Ensure <head> contains the CSS in a <style> block for clients that support media queries
        if (modifiedHtml.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            var cleanCss = CommentRegex.Replace(css, "").Trim();
            var styleTag = $"\n  <style>\n{cleanCss}\n  </style>\n</head>";
            modifiedHtml = Regex.Replace(modifiedHtml, "</head>", styleTag, RegexOptions.IgnoreCase);
        }

        return modifiedHtml;
    }

    /// <summary>
    /// Parses CSS string into a list of selectors and trimmed declarations.
    /// </summary>
    public static List<CssRule> ParseCss(string css)
    {
        var result = new List<CssRule>();
        if (string.IsNullOrWhiteSpace(css))
            return result;

        var withoutComments = CommentRegex.Replace(css, "");
        var matches = RuleRegex.Matches(withoutComments);

        foreach (Match match in matches)
        {
            var rawSelector = match.Groups[1].Value.Trim();
            var rawDeclarations = match.Groups[2].Value.Trim();

            // Skip @media queries or @keyframes in the inline mapper (they remain in <style>)
            if (rawSelector.StartsWith("@"))
                continue;

            // Normalize declarations into single line ending with ;
            var cleanDeclarations = Regex.Replace(rawDeclarations, @"\s+", " ")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();

            if (!cleanDeclarations.EndsWith(";"))
                cleanDeclarations += ";";

            // Support comma-separated selectors (e.g. "th, td")
            var selectors = rawSelector.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in selectors)
            {
                result.Add(new CssRule
                {
                    Selector = s.Trim(),
                    Declarations = cleanDeclarations
                });
            }
        }

        return result;
    }
}
