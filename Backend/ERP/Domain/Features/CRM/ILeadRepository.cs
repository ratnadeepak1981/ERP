using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public interface ILeadRepository
{
    Task<List<Lead>> GetLeadsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? assignedUserId = null,
        LeadStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<Lead?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsLeadNumberAsync(
        Guid tenantId,
        Guid companyId,
        string leadNumber,
        CancellationToken cancellationToken = default);

    Task AddAsync(Lead lead, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
