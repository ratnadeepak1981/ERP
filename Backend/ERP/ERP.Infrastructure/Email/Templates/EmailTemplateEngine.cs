using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ERP.Application.Common.Email;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Email.Templates;

/// <summary>
/// Centralised template rendering engine for email notifications across ApexERP modules.
/// Resolves template HTML and CSS from disk, injects dynamic placeholders, and inlines CSS styles.
/// </summary>
public class EmailTemplateEngine : IEmailTemplateEngine
{
    private readonly ILogger<EmailTemplateEngine> _logger;
    private readonly string _templateBaseDirectory;
    private readonly ConcurrentDictionary<string, (string Html, string Css)> _templateCache = new();

    public EmailTemplateEngine(ILogger<EmailTemplateEngine> logger, string? customTemplateDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _templateBaseDirectory = ResolveTemplateRoot(customTemplateDirectory);
        _logger.LogInformation("EmailTemplateEngine initialized with template root: {TemplateRoot}", _templateBaseDirectory);
    }

    public Task<string> RenderAsync<TModel>(
        string templateName,
        TModel model,
        CancellationToken cancellationToken = default)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (model is IDictionary<string, string?> dict)
        {
            return RenderWithPlaceholdersAsync(templateName, dict, cancellationToken);
        }

        var placeholders = ExtractPlaceholdersFromModel(model);
        return RenderWithPlaceholdersAsync(templateName, placeholders, cancellationToken);
    }

    public Task<string> RenderAsync(
        string templateName,
        IDictionary<string, string?> placeholders,
        CancellationToken cancellationToken = default)
    {
        return RenderWithPlaceholdersAsync(templateName, placeholders, cancellationToken);
    }

    private async Task<string> RenderWithPlaceholdersAsync(
        string templateName,
        IDictionary<string, string?> placeholders,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be empty.", nameof(templateName));

        var (rawHtml, rawCss) = await LoadTemplateFilesAsync(templateName, cancellationToken);

        // Replace placeholders
        var processedHtml = ReplacePlaceholders(rawHtml, placeholders);

        // Inline CSS
        var inlinedHtml = CssInliner.InlineCss(processedHtml, rawCss);
        return inlinedHtml;
    }

    private async Task<(string Html, string Css)> LoadTemplateFilesAsync(
        string templateName, 
        CancellationToken cancellationToken)
    {
        var normalizedKey = templateName.Trim().Replace('\\', '/');

        if (_templateCache.TryGetValue(normalizedKey, out var cached))
        {
            return cached;
        }

        var (htmlPath, cssPath) = ResolveTemplatePaths(normalizedKey);

        if (!File.Exists(htmlPath))
        {
            throw new FileNotFoundException($"Email template HTML file was not found at '{htmlPath}'.", htmlPath);
        }

        var html = await File.ReadAllTextAsync(htmlPath, cancellationToken);
        var css = File.Exists(cssPath) ? await File.ReadAllTextAsync(cssPath, cancellationToken) : string.Empty;

        var result = (html, css);
        _templateCache[normalizedKey] = result;
        return result;
    }

    private (string HtmlPath, string CssPath) ResolveTemplatePaths(string templateName)
    {
        // Support e.g. "PurchaseOrder" or "PurchaseOrder/PurchaseOrderEmail"
        var segments = templateName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var folderName = segments[0];
        var fileNameWithoutExt = segments.Length > 1 ? segments[^1] : $"{folderName}Email";

        var folderPath = Path.Combine(_templateBaseDirectory, folderName);

        // Check if direct file exists in template root (e.g. PurchaseOrder/PurchaseOrderEmail.html)
        var htmlPath = Path.Combine(folderPath, $"{fileNameWithoutExt}.html");
        var cssPath = Path.Combine(folderPath, $"{fileNameWithoutExt}.css");

        // Fallback: If not found, check if {folderName}.html exists
        if (!File.Exists(htmlPath))
        {
            var altHtml = Path.Combine(folderPath, $"{folderName}.html");
            if (File.Exists(altHtml))
            {
                htmlPath = altHtml;
                cssPath = Path.Combine(folderPath, $"{folderName}.css");
            }
        }

        return (htmlPath, cssPath);
    }

    private static string ReplacePlaceholders(string html, IDictionary<string, string?> placeholders)
    {
        if (placeholders == null || placeholders.Count == 0)
            return html;

        var result = html;
        foreach (var (key, value) in placeholders)
        {
            var encodedValue = WebUtility.HtmlEncode(value ?? string.Empty);
            // Matches {{Key}} and {{ Key }}
            var pattern = @"\{\{\s*" + Regex.Escape(key) + @"\s*\}\}";
            result = Regex.Replace(result, pattern, encodedValue, RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, string?> ExtractPlaceholdersFromModel<TModel>(TModel model)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (model == null) return dict;

        var targetType = model.GetType();
        var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.GetIndexParameters().Length > 0 || !prop.CanRead)
                continue;

            try
            {
                var value = prop.GetValue(model);
                dict[prop.Name] = value?.ToString();
            }
            catch
            {
                // Safely skip any property that fails evaluation
            }
        }

        return dict;
    }

    private static string ResolveTemplateRoot(string? customDirectory)
    {
        if (!string.IsNullOrWhiteSpace(customDirectory) && Directory.Exists(customDirectory))
            return customDirectory;

        // 1. Next to binary (AppContext.BaseDirectory/Views/Templates)
        var binTemplates = Path.Combine(AppContext.BaseDirectory, "Views", "Templates");
        if (Directory.Exists(binTemplates))
            return binTemplates;

        // 2. Search upwards from AppContext.BaseDirectory or CurrentDirectory for Views/Templates or Backend/Views/Templates
        var searchRoots = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var root in searchRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate1 = Path.Combine(dir.FullName, "Views", "Templates");
                if (Directory.Exists(candidate1)) return candidate1;

                var candidate2 = Path.Combine(dir.FullName, "Backend", "Views", "Templates");
                if (Directory.Exists(candidate2)) return candidate2;

                dir = dir.Parent;
            }
        }

        // 3. Fallbacks
        var fallbacks = new[]
        {
            @"\\WIN-L5P4VHG1LF0\Students$\UT011647\ERP\Backend\Views\Templates",
            @"Z:\ERP\Backend\Views\Templates",
            @"Z:\Backend\Views\Templates"
        };
        foreach (var p in fallbacks)
        {
            if (Directory.Exists(p)) return p;
        }

        return binTemplates;
    }
}
