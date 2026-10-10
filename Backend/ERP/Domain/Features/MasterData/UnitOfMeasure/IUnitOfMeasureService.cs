using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.UnitOfMeasure;

public interface IUnitOfMeasureService
{
    Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId);
    Task<UnitOfMeasure?> GetUnitAsync(Guid tenantId, Guid unitOfMeasureId);
    Task<UnitOfMeasure> CreateUnitAsync(Guid tenantId, CreateUnitOfMeasureRequest request);
    Task<UnitOfMeasure> UpdateUnitAsync(Guid tenantId, Guid unitOfMeasureId, UpdateUnitOfMeasureRequest request);
    Task DeactivateUnitAsync(Guid tenantId, Guid unitOfMeasureId);

    Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId);
    Task<UnitOfMeasureConversion> CreateConversionAsync(Guid tenantId, CreateUnitOfMeasureConversionRequest request);
    Task<UnitOfMeasureConversion> UpdateConversionAsync(Guid tenantId, Guid conversionId, UpdateUnitOfMeasureConversionRequest request);
    Task DeactivateConversionAsync(Guid tenantId, Guid conversionId);
}
