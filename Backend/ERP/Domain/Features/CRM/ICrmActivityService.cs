using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public interface ICrmActivityService
{
    Task<List<CrmActivityDto>> GetActivitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? customerId = null,
        bool? isCompleted = null,
        CancellationToken cancellationToken = default);

    Task<CrmActivityDto?> GetActivityByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        CancellationToken cancellationToken = default);

    Task<CrmActivityDto> CreateActivityAsync(
        Guid tenantId,
        Guid companyId,
        CreateCrmActivityRequest request,
        CancellationToken cancellationToken = default);

    Task<CrmActivityDto> UpdateActivityAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        UpdateCrmActivityRequest request,
        CancellationToken cancellationToken = default);

    Task<CrmActivityDto> CompleteActivityAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        CompleteCrmActivityRequest request,
        CancellationToken cancellationToken = default);
}
