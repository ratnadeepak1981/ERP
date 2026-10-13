using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public interface IOpportunityRepository
{
    Task<List<Opportunity>> GetOpportunitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? customerId = null,
        OpportunityStage? stage = null,
        CancellationToken cancellationToken = default);

    Task<Opportunity?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsOpportunityNumberAsync(
        Guid tenantId,
        Guid companyId,
        string opportunityNumber,
        CancellationToken cancellationToken = default);

    Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
