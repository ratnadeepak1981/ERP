using System;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.UnitOfMeasure;

public interface ISpecialUomConversionSettingsProvider
{
    Task<bool> IsSpecialUomConversionEnabledAsync(Guid tenantId, Guid? companyId = null);
}
