using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Sales.SalesQuotation;

namespace ERP.Application.Features.Sales.SalesQuotation;

public sealed record SalesQuotationEmailDispatchResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    string? RecipientEmail = null,
    string? ViewUrl = null);

public interface ISalesQuotationEmailService
{
    Task<SalesQuotationEmailDispatchResult> SendQuotationNotificationAsync(
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation,
        CancellationToken ct = default);
}
