using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Domain.Features.MasterData.Common;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductEntity = Domain.Features.MasterData.Product.Product;

namespace ERP.Application.Features.MasterData.Product;

public class ProductIdentifierService : IProductIdentifierService
{
    private readonly IProductIdentifierRepository _repository;
    private readonly IUnitOfMeasureService _uomService;
    private readonly ILogger<ProductIdentifierService>? _logger;

    public ProductIdentifierService(
        IProductIdentifierRepository repository,
        IUnitOfMeasureService uomService,
        ILogger<ProductIdentifierService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _uomService = uomService ?? throw new ArgumentNullException(nameof(uomService));
        _logger = logger;
    }

    public async Task<List<ProductIdentifierDto>> GetIdentifiersAsync(Guid tenantId, Guid productId, bool includeInactive = false)
    {
        var product = await _repository.GetProductAsync(tenantId, productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in the current tenant scope.");

        var list = await _repository.GetIdentifiersAsync(tenantId, productId, includeInactive);

        var uomIds = list.Where(i => i.UnitOfMeasureId.HasValue).Select(i => i.UnitOfMeasureId!.Value).Distinct().ToList();
        var uoms = uomIds.Any()
            ? await _repository.GetUomCodesAsync(tenantId, uomIds)
            : new Dictionary<Guid, string>();

        return list.Select(i => MapToDto(i, i.UnitOfMeasureId.HasValue && uoms.TryGetValue(i.UnitOfMeasureId.Value, out var code) ? code : null)).ToList();
    }

    public async Task<ProductIdentifierDto?> GetIdentifierByIdAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, productId, identifierId);

        if (identifier == null) return null;

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            uomCode = await _repository.GetUomCodeAsync(tenantId, identifier.UnitOfMeasureId.Value);
        }

        return MapToDto(identifier, uomCode);
    }

    public async Task<ProductIdentifierDto> AddIdentifierAsync(Guid tenantId, Guid productId, CreateProductIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var product = await _repository.GetProductAsync(tenantId, productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in the current tenant scope.");

        string normalizedValue = request.IdentifierValue.Trim().ToUpperInvariant();
        SymbologyValidator.Validate(request.Symbology, normalizedValue);

        await ValidateUomCompatibilityAsync(tenantId, product, request.UnitOfMeasureId);

        // Value uniqueness check for active identifiers in this tenant
        bool exists = await _repository.ExistsActiveValueAsync(tenantId, normalizedValue);
        if (exists)
        {
            throw new InvalidOperationException($"Identifier value '{normalizedValue}' is already registered to an active product in this tenant.");
        }

        // Manage primary promotion atomically
        if (request.IsPrimary)
        {
            await _repository.DemoteExistingPrimariesAsync(tenantId, productId, request.UnitOfMeasureId);
            await _repository.SaveChangesAsync();
        }

        var identifier = ProductIdentifier.Create(
            tenantId,
            productId,
            normalizedValue,
            request.IdentifierType,
            request.Symbology,
            request.Source,
            request.UnitOfMeasureId,
            request.IsPrimary);

        await _repository.AddIdentifierAsync(identifier);
        await _repository.SaveChangesAsync();

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            uomCode = await _repository.GetUomCodeAsync(tenantId, identifier.UnitOfMeasureId.Value);
        }

        return MapToDto(identifier, uomCode);
    }

    public async Task<ProductIdentifierDto> GenerateInternalIdentifierAsync(Guid tenantId, Guid productId, GenerateInternalProductIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var product = await _repository.GetProductAsync(tenantId, productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in the current tenant scope.");

        await ValidateUomCompatibilityAsync(tenantId, product, request.UnitOfMeasureId);

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            string candidate = GenerateCandidateCode(tenantId, productId, request.Symbology, request.UnitOfMeasureId);

            // Pre-check existence
            bool exists = await _repository.ExistsActiveValueAsync(tenantId, candidate);
            if (exists)
            {
                _logger?.LogWarning("Candidate collision detected for '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                continue;
            }

            if (request.IsPrimary)
            {
                await _repository.DemoteExistingPrimariesAsync(tenantId, productId, request.UnitOfMeasureId);
                await _repository.SaveChangesAsync();
            }

            var identifier = ProductIdentifier.Create(
                tenantId,
                productId,
                candidate,
                request.IdentifierType,
                request.Symbology,
                IdentifierSource.Internal,
                request.UnitOfMeasureId,
                request.IsPrimary);

            try
            {
                await _repository.AddIdentifierAsync(identifier);
                await _repository.SaveChangesAsync();

                string? uomCode = null;
                if (identifier.UnitOfMeasureId.HasValue)
                {
                    uomCode = await _repository.GetUomCodeAsync(tenantId, identifier.UnitOfMeasureId.Value);
                }

                return MapToDto(identifier, uomCode);
            }
            catch (DbUpdateException ex) when (attempt < maxRetries)
            {
                _logger?.LogWarning(ex, "DbUpdateException on candidate '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                _repository.Detach(identifier);
            }
        }

        throw new InvalidOperationException("Failed to generate a unique internal identifier after multiple attempts due to uniqueness collision.");
    }

    public async Task<ProductIdentifierDto> SetPrimaryAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, productId, identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for product '{productId}'.");

        if (!identifier.IsActive)
        {
            throw new InvalidOperationException("Cannot set an inactive identifier as primary.");
        }

        if (identifier.IsPrimary)
        {
            return MapToDto(identifier, null);
        }

        // Atomically demote any existing primary for this product and UOM
        await _repository.DemoteExistingPrimariesAsync(tenantId, productId, identifier.UnitOfMeasureId);
        await _repository.SaveChangesAsync();

        identifier.PromoteToPrimary();
        await _repository.SaveChangesAsync();

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            uomCode = await _repository.GetUomCodeAsync(tenantId, identifier.UnitOfMeasureId.Value);
        }

        return MapToDto(identifier, uomCode);
    }

    public async Task DeactivateIdentifierAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, productId, identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for product '{productId}'.");

        identifier.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<ProductIdentifierDto> ReactivateIdentifierAsync(Guid tenantId, Guid productId, Guid identifierId, bool isPrimary = false)
    {
        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, productId, identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for product '{productId}'.");

        if (identifier.IsActive)
        {
            return MapToDto(identifier, null);
        }

        // Verify that no active identifier currently holds this value in the tenant
        bool exists = await _repository.ExistsActiveValueAsync(tenantId, identifier.IdentifierValue);
        if (exists)
        {
            throw new InvalidOperationException($"Cannot reactivate identifier: value '{identifier.IdentifierValue}' is currently in use by another active item.");
        }

        if (isPrimary)
        {
            await _repository.DemoteExistingPrimariesAsync(tenantId, productId, identifier.UnitOfMeasureId);
        }

        identifier.Reactivate(isPrimary);
        await _repository.SaveChangesAsync();

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            uomCode = await _repository.GetUomCodeAsync(tenantId, identifier.UnitOfMeasureId.Value);
        }

        return MapToDto(identifier, uomCode);
    }

    private async Task ValidateUomCompatibilityAsync(Guid tenantId, ProductEntity product, Guid? requestedUomId)
    {
        if (!requestedUomId.HasValue) return;

        // If product has a base stock UOM and it matches requested UOM, valid
        if (product.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.Value == requestedUomId.Value)
        {
            return;
        }

        // Check if requested UOM exists in tenant
        var requestedUom = await _repository.GetUomByIdAsync(tenantId, requestedUomId.Value)
            ?? throw new ArgumentException($"Unit of Measure '{requestedUomId.Value}' does not exist in this tenant.", nameof(requestedUomId));

        if (!product.UnitOfMeasureId.HasValue || requestedUomId.Value == product.UnitOfMeasureId.Value)
        {
            // Product has no base UOM specified, or requested UOM is the product's base stock UOM
            return;
        }

        // Verify conversion exists between requested UOM and product base UOM
        var conversions = await _uomService.GetConversionsAsync(tenantId);
        bool hasConversion = conversions.Any(c =>
            c.IsActive &&
            ((c.FromUnitId == requestedUomId.Value && c.ToUnitId == product.UnitOfMeasureId.Value) ||
             (c.FromUnitId == product.UnitOfMeasureId.Value && c.ToUnitId == requestedUomId.Value)));

        if (!hasConversion)
        {
            throw new ArgumentException(
                $"Unit of Measure '{requestedUom.Code}' is not convertible to the product's base stock Unit of Measure.");
        }
    }

    private static string GenerateCandidateCode(Guid tenantId, Guid productId, IdentifierSymbology symbology, Guid? unitOfMeasureId)
    {
        if (symbology == IdentifierSymbology.QRCode)
        {
            return unitOfMeasureId.HasValue
                ? $"urn:apex:v1:prd:{tenantId:N}:{productId:N}:{unitOfMeasureId.Value:N}"
                : $"urn:apex:v1:prd:{tenantId:N}:{productId:N}";
        }

        // Code 128 internal candidate: APX-P-YYMMDD-XXXXX-C
        byte[] randomBytes = new byte[4];
        RandomNumberGenerator.Fill(randomBytes);
        string hex = BitConverter.ToString(randomBytes).Replace("-", "").Substring(0, 5).ToUpperInvariant();
        string baseStr = $"APX-P-{DateTime.UtcNow:yyMMdd}-{hex}";

        int check = Math.Abs(baseStr.GetHashCode()) % 10;
        return $"{baseStr}-{check}";
    }

    private static ProductIdentifierDto MapToDto(ProductIdentifier entity, string? uomCode)
    {
        return new ProductIdentifierDto
        {
            ProductIdentifierId = entity.ProductIdentifierId,
            TenantId = entity.TenantId,
            ProductId = entity.ProductId,
            UnitOfMeasureId = entity.UnitOfMeasureId,
            UnitOfMeasureCode = uomCode,
            IdentifierValue = entity.IdentifierValue,
            IdentifierType = entity.IdentifierType,
            Symbology = entity.Symbology,
            Source = entity.Source,
            IsPrimary = entity.IsPrimary,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        };
    }
}
