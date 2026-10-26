using System;

namespace Domain.Features.MasterData.PaymentTerm;

public class PaymentTermDto
{
    public Guid PaymentTermId { get; set; }
    public Guid TenantId { get; set; }
    public string TermCode { get; set; } = string.Empty;
    public string TermName { get; set; } = string.Empty;
    public int Days { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePaymentTermRequest
{
    public string TermCode { get; set; } = string.Empty;
    public string TermName { get; set; } = string.Empty;
    public int Days { get; set; }
    public string? Description { get; set; }
}

public class UpdatePaymentTermRequest
{
    public string TermName { get; set; } = string.Empty;
    public int Days { get; set; }
    public string? Description { get; set; }
}
