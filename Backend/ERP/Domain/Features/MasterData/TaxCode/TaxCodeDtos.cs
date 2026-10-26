using System;

namespace Domain.Features.MasterData.TaxCode;

public class TaxCodeDto
{
    public Guid TaxCodeId { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public TaxApplicability Applicability { get; set; }
    public string ApplicabilityName => Applicability.ToString();
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTaxCodeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
}

public class UpdateTaxCodeRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public TaxApplicability Applicability { get; set; } = TaxApplicability.Both;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
}
