using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Common.Email;

/// <summary>
/// Abstraction for rendering named email templates into final, inlined, email-compatible HTML.
/// </summary>
public interface IEmailTemplateEngine
{
    /// <summary>
    /// Renders a template by name using the properties of a strongly typed model object.
    /// </summary>
    /// <typeparam name="TModel">Model type containing placeholder replacement values.</typeparam>
    /// <param name="templateName">Relative or logical template name (e.g. "PurchaseOrder/PurchaseOrderEmail").</param>
    /// <param name="model">The data model object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered HTML with CSS styles inlined into tags.</returns>
    Task<string> RenderAsync<TModel>(
        string templateName,
        TModel model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders a template by name using an explicit key-value dictionary of placeholders.
    /// </summary>
    /// <param name="templateName">Relative or logical template name (e.g. "PurchaseOrder/PurchaseOrderEmail").</param>
    /// <param name="placeholders">Key-value dictionary of placeholders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered HTML with CSS styles inlined into tags.</returns>
    Task<string> RenderAsync(
        string templateName,
        IDictionary<string, string?> placeholders,
        CancellationToken cancellationToken = default);
}
