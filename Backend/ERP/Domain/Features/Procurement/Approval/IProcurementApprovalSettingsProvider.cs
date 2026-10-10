using System;

namespace Domain.Features.Procurement.Approval;

public class ProcurementApprovalSettings
{
    public bool ApprovalRequired { get; set; } = true;

    public int ApprovalMode { get; set; } = 1; // 1 = Simple, 2 = Advanced
}

public interface IProcurementApprovalSettingsProvider
{
    Task<ProcurementApprovalSettings> GetSettingsAsync(Guid tenantId);
}
