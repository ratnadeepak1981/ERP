using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.RequestForQuotation;

public enum RFQSupplierInvitationStatus
{
    Pending = 1,
    Sent = 2,
    Opened = 3,
    Submitted = 4,
    Declined = 5,
    Failed = 6,
    Sending = 7
}

public class RFQSupplier : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid RFQId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    public RFQSupplierInvitationStatus InvitationStatus { get; set; } = RFQSupplierInvitationStatus.Pending;
    public DateTime? InvitedAt { get; set; }

    public string? TokenHash { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? ResponseReceivedAt { get; set; }

    public RequestForQuotation? RequestForQuotation { get; set; }
}
