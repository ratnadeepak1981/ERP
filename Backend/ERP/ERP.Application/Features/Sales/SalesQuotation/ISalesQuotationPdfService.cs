using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Features.Sales.SalesQuotation;

public interface ISalesQuotationPdfService
{
    Task<byte[]> GeneratePdfForQuotationAsync(
        Guid tenantId,
        Guid quotationId,
        CancellationToken cancellationToken = default);
}
