using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM.Deduplication;

public interface ICustomerDeduplicationService
{
    Task<CustomerMatchResult> FindDuplicateAsync(
        Guid tenantId,
        CustomerMatchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<List<CustomerMatchResult>> FindAllPotentialMatchesAsync(
        Guid tenantId,
        CustomerMatchCriteria criteria,
        CancellationToken cancellationToken = default);
}
