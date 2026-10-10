using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.UnitOfMeasure;

public interface IUnitOfMeasureRepository
{
    Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId);
    Task<UnitOfMeasure?> GetUnitByIdAsync(Guid tenantId, Guid unitOfMeasureId);
    Task<UnitOfMeasure?> GetUnitByCodeAsync(Guid tenantId, string code);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code);
    Task AddUnitAsync(UnitOfMeasure unitOfMeasure);

    Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId);
    Task<UnitOfMeasureConversion?> GetConversionByIdAsync(Guid tenantId, Guid conversionId);
    Task<UnitOfMeasureConversion?> GetConversionAsync(Guid tenantId, Guid fromUnitId, Guid toUnitId);
    Task AddConversionAsync(UnitOfMeasureConversion conversion);

    Task SaveChangesAsync();
}
