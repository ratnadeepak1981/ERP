using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.PaymentTerm;

public class PaymentTerm : Auditable, ITenantScopedEntity
{
    public Guid PaymentTermId { get; private set; }

    public Guid TenantId { get; private set; }

    public string TermCode { get; private set; } = string.Empty;

    public string TermName { get; private set; } = string.Empty;

    public int Days { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static PaymentTerm Create(
        Guid tenantId,
        string termCode,
        string termName,
        int days = 0,
        string? description = null,
        Guid? paymentTermId = null)
    {
        if (days < 0)
            throw new ArgumentException("Days cannot be negative.", nameof(days));

        return new PaymentTerm
        {
            PaymentTermId = paymentTermId ?? Guid.NewGuid(),
            TenantId = tenantId,
            TermCode = termCode.Trim().ToUpperInvariant(),
            TermName = termName.Trim(),
            Days = days,
            Description = description?.Trim(),
            IsActive = true
        };
    }

    public void Update(string termName, int days, string? description)
    {
        if (days < 0)
            throw new ArgumentException("Days cannot be negative.", nameof(days));

        TermName = termName.Trim();
        Days = days;
        Description = description?.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private PaymentTerm()
    {
    }
}
