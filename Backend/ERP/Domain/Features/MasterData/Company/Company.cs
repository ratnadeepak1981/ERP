using System;
using System.ComponentModel.DataAnnotations.Schema;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Company;

public class Company : ITenantScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool SpecialUomConversionEnabled { get; set; } = true;

    public Guid? BaseCurrencyId { get; set; }

    public Currency.Currency? BaseCurrency { get; set; }

    [NotMapped]
    public string? BaseCurrencyCode => BaseCurrency?.CurrencyCode;
}
