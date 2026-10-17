using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public interface IOpportunityService
{
    Task<List<OpportunityDto>> GetOpportunitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? customerId = null,
        OpportunityStage? stage = null,
        CancellationToken cancellationToken = default);

    Task<OpportunityDto?> GetOpportunityByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        CancellationToken cancellationToken = default);

    Task<OpportunityDto> CreateOpportunityAsync(
        Guid tenantId,
        Guid companyId,
        CreateOpportunityRequest request,
        CancellationToken cancellationToken = default);

    Task<OpportunityDto> UpdateOpportunityAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        UpdateOpportunityRequest request,
        CancellationToken cancellationToken = default);

    Task<OpportunityDto> UpdateOpportunityStageAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        UpdateOpportunityStageRequest request,
        CancellationToken cancellationToken = default);
}
