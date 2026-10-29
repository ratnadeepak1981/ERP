using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Domain.Features.MasterData.Common;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domain.Features.MasterData.Product;

public class ProductIdentifierService : IProductIdentifierService
{
    private readonly DomainDbContext _context;
    private readonly IUnitOfMeasureService _uomService;
    private readonly ILogger<ProductIdentifierService>? _logger;

    public ProductIdentifierService(
        DomainDbContext context,
        IUnitOfMeasureService uomService,
        ILogger<ProductIdentifierService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _uomService = uomService ?? throw new ArgumentNullException(nameof(uomService));
        _logger = logger;
    }

    public async Task<List<ProductIdentifierDto>> GetIdentifiersAsync(Guid tenantId, Guid productId, bool includeInactive = false)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in the current tenant scope.");

        var query = _context.ProductIdentifiers
            .AsNoTracking()
            .Where(pi => pi.TenantId == tenantId && pi.ProductId == productId);

        if (!includeInactive)
        {
            query = query.Where(pi => pi.IsActive);
        }

        var list = await query.OrderByDescending(pi => pi.IsPrimary).ThenBy(pi => pi.CreatedAt).ToListAsync();

        var uomIds = list.Where(i => i.UnitOfMeasureId.HasValue).Select(i => i.UnitOfMeasureId!.Value).Distinct().ToList();
        var uoms = uomIds.Any()
            ? await _context.UnitsOfMeasure.AsNoTracking().Where(u => u.TenantId == tenantId && uomIds.Contains(u.UnitOfMeasureId)).ToDictionaryAsync(u => u.UnitOfMeasureId, u => u.Code)
            : new Dictionary<Guid, string>();

        return list.Select(i => MapToDto(i, i.UnitOfMeasureId.HasValue && uoms.TryGetValue(i.UnitOfMeasureId.Value, out var code) ? code : null)).ToList();
    }

    public async Task<ProductIdentifierDto?> GetIdentifierByIdAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        var identifier = await _context.ProductIdentifiers
            .AsNoTracking()
            .FirstOrDefaultAsync(pi => pi.TenantId == tenantId && pi.ProductId == productId && pi.ProductIdentifierId == identifierId);

        if (identifier == null) return null;

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            var uom = await _context.UnitsOfMeasure.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == identifier.UnitOfMeasureId.Value);
            uomCode = uom?.Code;
        }

        return MapToDto(identifier, uomCode);
    }

    public async Task<ProductIdentifierDto> AddIdentifierAsync(Guid tenantId, Guid productId, CreateProductIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in the current tenant scope.");

        string normalizedValue = request.IdentifierValue.Trim().ToUpperInvariant();
        SymbologyValidator.Validate(request.Symbology, normalizedValue);

        await ValidateUomCompatibilityAsync(tenantId, product, request.UnitOfMeasureId);

        // Value uniqueness check for active identifiers in this tenant
        bool exists = await _context.ProductIdentifiers
            .AnyAsync(pi => pi.TenantId == tenantId && pi.IdentifierValue == normalizedValue && pi.IsActive);
        if (exists)
        {
            throw new InvalidOperationException($"Identifier value '{normalizedValue}' is already registered to an active product in this tenant.");
        }

        // Manage primary promotion atomically
        if (request.IsPrimary)
        {
            await DemoteExistingPrimariesAsync(tenantId, productId, request.UnitOfMeasureId);
            await _context.SaveChangesAsync();
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

        await _context.ProductIdentifiers.AddAsync(identifier);
        await _context.SaveChangesAsync();

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            var uom = await _context.UnitsOfMeasure.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == identifier.UnitOfMeasureId.Value);
            uomCode = uom?.Code;
        }

        return MapToDto(identifier, uomCode);
    }

    public async Task<ProductIdentifierDto> GenerateInternalIdentifierAsync(Guid tenantId, Guid productId, GenerateInternalProductIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId)
            ?? throw new KeyNotFoundException($"Product '{productId}' not found in the current tenant scope.");

        await ValidateUomCompatibilityAsync(tenantId, product, request.UnitOfMeasureId);

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            string candidate = GenerateCandidateCode(tenantId, productId, request.Symbology, request.UnitOfMeasureId);

            // Pre-check existence
            bool exists = await _context.ProductIdentifiers
                .AnyAsync(pi => pi.TenantId == tenantId && pi.IdentifierValue == candidate && pi.IsActive);
            if (exists)
            {
                _logger?.LogWarning("Candidate collision detected for '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                continue;
            }

            if (request.IsPrimary)
            {
                await DemoteExistingPrimariesAsync(tenantId, productId, request.UnitOfMeasureId);
                await _context.SaveChangesAsync();
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
                await _context.ProductIdentifiers.AddAsync(identifier);
                await _context.SaveChangesAsync();

                string? uomCode = null;
                if (identifier.UnitOfMeasureId.HasValue)
                {
                    var uom = await _context.UnitsOfMeasure.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == identifier.UnitOfMeasureId.Value);
                    uomCode = uom?.Code;
                }

                return MapToDto(identifier, uomCode);
            }
            catch (DbUpdateException ex) when (attempt < maxRetries)
            {
                _logger?.LogWarning(ex, "DbUpdateException on candidate '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                // Detach failed entity before retrying
                _context.Entry(identifier).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique internal identifier after multiple attempts due to uniqueness collision.");
    }

    public async Task<ProductIdentifierDto> SetPrimaryAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        var identifier = await _context.ProductIdentifiers
            .FirstOrDefaultAsync(pi => pi.TenantId == tenantId && pi.ProductId == productId && pi.ProductIdentifierId == identifierId)
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
        await DemoteExistingPrimariesAsync(tenantId, productId, identifier.UnitOfMeasureId);
        await _context.SaveChangesAsync();

        identifier.PromoteToPrimary();
        await _context.SaveChangesAsync();

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            var uom = await _context.UnitsOfMeasure.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == identifier.UnitOfMeasureId.Value);
            uomCode = uom?.Code;
        }

        return MapToDto(identifier, uomCode);
    }

    public async Task DeactivateIdentifierAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        var identifier = await _context.ProductIdentifiers
            .FirstOrDefaultAsync(pi => pi.TenantId == tenantId && pi.ProductId == productId && pi.ProductIdentifierId == identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for product '{productId}'.");

        identifier.Deactivate();
        await _context.SaveChangesAsync();
    }

    public async Task<ProductIdentifierDto> ReactivateIdentifierAsync(Guid tenantId, Guid productId, Guid identifierId, bool isPrimary = false)
    {
        var identifier = await _context.ProductIdentifiers
            .FirstOrDefaultAsync(pi => pi.TenantId == tenantId && pi.ProductId == productId && pi.ProductIdentifierId == identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for product '{productId}'.");

        if (identifier.IsActive)
        {
            return MapToDto(identifier, null);
        }

        // Verify that no active identifier currently holds this value in the tenant
        bool exists = await _context.ProductIdentifiers
            .AnyAsync(pi => pi.TenantId == tenantId && pi.IdentifierValue == identifier.IdentifierValue && pi.IsActive);
        if (exists)
        {
            throw new InvalidOperationException($"Cannot reactivate identifier: value '{identifier.IdentifierValue}' is currently in use by another active item.");
        }

        if (isPrimary)
        {
            await DemoteExistingPrimariesAsync(tenantId, productId, identifier.UnitOfMeasureId);
        }

        identifier.Reactivate(isPrimary);
        await _context.SaveChangesAsync();

        string? uomCode = null;
        if (identifier.UnitOfMeasureId.HasValue)
        {
            var uom = await _context.UnitsOfMeasure.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == identifier.UnitOfMeasureId.Value);
            uomCode = uom?.Code;
        }

        return MapToDto(identifier, uomCode);
    }

    private async Task ValidateUomCompatibilityAsync(Guid tenantId, Product product, Guid? requestedUomId)
    {
        if (!requestedUomId.HasValue) return;

        // If product has a base stock UOM and it matches requested UOM, valid
        if (product.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.Value == requestedUomId.Value)
        {
            return;
        }

        // Check if requested UOM exists in tenant
        var requestedUom = await _context.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == requestedUomId.Value)
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

    private async Task DemoteExistingPrimariesAsync(Guid tenantId, Guid productId, Guid? unitOfMeasureId)
    {
        var existingPrimaries = await _context.ProductIdentifiers
            .Where(pi => pi.TenantId == tenantId &&
                         pi.ProductId == productId &&
                         pi.UnitOfMeasureId == unitOfMeasureId &&
                         pi.IsPrimary &&
                         pi.IsActive)
            .ToListAsync();

        foreach (var primary in existingPrimaries)
        {
            primary.DemoteFromPrimary();
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
