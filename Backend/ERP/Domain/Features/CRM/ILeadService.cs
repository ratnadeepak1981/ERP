using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Domain.Features.CRM;

public interface ILeadService
{
    Task<List<LeadDto>> GetLeadsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? assignedUserId = null,
        LeadStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<LeadDto?> GetLeadByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task<LeadDto> CreateLeadAsync(
        Guid tenantId,
        Guid companyId,
        CreateLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<LeadDto> UpdateLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        UpdateLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<LeadDto> AssignLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        AssignLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<LeadDto> QualifyLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        CancellationToken cancellationToken = default);

    Task<LeadDto> DisqualifyLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        DisqualifyLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<ConvertLeadResult> ConvertLeadAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        ConvertLeadRequest request,
        CancellationToken cancellationToken = default);
}
