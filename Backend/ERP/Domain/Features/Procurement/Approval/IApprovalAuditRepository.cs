using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.Approval;

public interface IApprovalAuditRepository
{
    Task AddRecordAsync(ApprovalAuditRecord record);
    Task<List<ApprovalAuditRecord>> GetRecordsAsync(Guid tenantId, string documentType, Guid documentId);
    Task SaveChangesAsync();
}
