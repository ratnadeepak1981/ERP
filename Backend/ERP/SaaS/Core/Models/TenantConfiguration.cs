using SaaS.Common;

namespace SaaS.Core.Models;

public class TenantConfiguration : Auditable
{
    public Guid TenantConfigurationId { get; private set; }

    public Guid TenantId { get; private set; }

    public bool CompanyEnabled { get; private set; }

    public bool BranchEnabled { get; private set; }

    public bool WarehouseZoneEnabled { get; private set; }

    public bool WarehouseBinEnabled { get; private set; }

    public bool ProcurementApprovalRequired { get; private set; } = true;

    public int ProcurementApprovalMode { get; private set; } = 1; // 1 = Simple, 2 = Advanced

    private TenantConfiguration()
    {
    }

    public static TenantConfiguration CreateDefault(
        Guid tenantId)
    {
        return new TenantConfiguration
        {
            TenantConfigurationId = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyEnabled = true,
            BranchEnabled = true,
            WarehouseZoneEnabled = true,
            WarehouseBinEnabled = true,
            ProcurementApprovalRequired = true,
            ProcurementApprovalMode = 1
        };
    }

    public void UpdateProcurementApproval(bool approvalRequired, int approvalMode)
    {
        if (approvalMode != 1 && approvalMode != 2)
        {
            throw new ArgumentException("Invalid approval mode.", nameof(approvalMode));
        }

        if (approvalMode == 2)
        {
            throw new InvalidOperationException("Advanced approval mode is not supported. Only Simple approval is supported.");
        }

        ProcurementApprovalRequired = approvalRequired;
        ProcurementApprovalMode = approvalMode;
    }
}