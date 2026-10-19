using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public interface ICrmActivityRepository
{
    Task<List<CrmActivity>> GetActivitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? customerId = null,
        bool? isCompleted = null,
        CancellationToken cancellationToken = default);

    Task<CrmActivity?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CrmActivity activity, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
