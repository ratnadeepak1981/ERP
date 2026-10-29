using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Domain.Features.MasterData.Common;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class LocationIdentifierService : ILocationIdentifierService
{
    private readonly DomainDbContext _context;
    private readonly ILogger<LocationIdentifierService>? _logger;

    public LocationIdentifierService(
        DomainDbContext context,
        ILogger<LocationIdentifierService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public async Task<List<LocationIdentifierDto>> GetIdentifiersAsync(Guid tenantId, Guid warehouseId, Guid locationId, bool includeInactive = false)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var query = _context.LocationIdentifiers
            .AsNoTracking()
            .Where(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId);

        if (!includeInactive)
        {
            query = query.Where(li => li.IsActive);
        }

        var list = await query.OrderByDescending(li => li.IsPrimary).ThenBy(li => li.CreatedAt).ToListAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<LocationIdentifierDto?> GetIdentifierByIdAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _context.LocationIdentifiers
            .AsNoTracking()
            .FirstOrDefaultAsync(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId && li.LocationIdentifierId == identifierId);

        return identifier == null ? null : MapToDto(identifier);
    }

    public async Task<LocationIdentifierDto> AddIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, CreateLocationIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        string normalized = request.IdentifierValue.Trim().ToUpperInvariant();
        SymbologyValidator.Validate(request.Symbology, normalized);

        // Value uniqueness check for active identifiers in this tenant
        bool exists = await _context.LocationIdentifiers
            .AnyAsync(li => li.TenantId == tenantId && li.IdentifierValue == normalized && li.IsActive);
        if (exists)
        {
            throw new InvalidOperationException($"Location identifier value '{normalized}' is already registered to an active location in this tenant.");
        }

        if (request.IsPrimary)
        {
            await DemoteExistingPrimariesAsync(tenantId, locationId);
            await _context.SaveChangesAsync();
        }

        var identifier = LocationIdentifier.Create(
            tenantId,
            warehouseId,
            locationId,
            normalized,
            request.IdentifierType,
            request.Symbology,
            request.Source,
            request.IsPrimary);

        await _context.LocationIdentifiers.AddAsync(identifier);
        await _context.SaveChangesAsync();

        return MapToDto(identifier);
    }

    public async Task<LocationIdentifierDto> GenerateInternalIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, GenerateInternalLocationIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            string candidate = GenerateCandidateCode(tenantId, locationId, request.Symbology);

            bool exists = await _context.LocationIdentifiers
                .AnyAsync(li => li.TenantId == tenantId && li.IdentifierValue == candidate && li.IsActive);
            if (exists)
            {
                _logger?.LogWarning("Candidate collision detected for location '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                continue;
            }

            if (request.IsPrimary)
            {
                await DemoteExistingPrimariesAsync(tenantId, locationId);
                await _context.SaveChangesAsync();
            }

            var identifier = LocationIdentifier.Create(
                tenantId,
                warehouseId,
                locationId,
                candidate,
                request.IdentifierType,
                request.Symbology,
                IdentifierSource.Internal,
                request.IsPrimary);

            try
            {
                await _context.LocationIdentifiers.AddAsync(identifier);
                await _context.SaveChangesAsync();
                return MapToDto(identifier);
            }
            catch (DbUpdateException ex) when (attempt < maxRetries)
            {
                _logger?.LogWarning(ex, "DbUpdateException on location candidate '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                _context.Entry(identifier).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique internal location identifier after multiple attempts due to uniqueness collision.");
    }

    public async Task<LocationIdentifierDto> SetPrimaryAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _context.LocationIdentifiers
            .FirstOrDefaultAsync(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId && li.LocationIdentifierId == identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for location '{locationId}'.");

        if (!identifier.IsActive)
        {
            throw new InvalidOperationException("Cannot set an inactive location identifier as primary.");
        }

        if (identifier.IsPrimary)
        {
            return MapToDto(identifier);
        }

        await DemoteExistingPrimariesAsync(tenantId, locationId);
        await _context.SaveChangesAsync();

        identifier.PromoteToPrimary();
        await _context.SaveChangesAsync();

        return MapToDto(identifier);
    }

    public async Task DeactivateIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _context.LocationIdentifiers
            .FirstOrDefaultAsync(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId && li.LocationIdentifierId == identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for location '{locationId}'.");

        identifier.Deactivate();
        await _context.SaveChangesAsync();
    }

    public async Task<LocationIdentifierDto> ReactivateIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId, bool isPrimary = false)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _context.LocationIdentifiers
            .FirstOrDefaultAsync(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId && li.LocationIdentifierId == identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for location '{locationId}'.");

        if (identifier.IsActive)
        {
            return MapToDto(identifier);
        }

        bool exists = await _context.LocationIdentifiers
            .AnyAsync(li => li.TenantId == tenantId && li.IdentifierValue == identifier.IdentifierValue && li.IsActive);
        if (exists)
        {
            throw new InvalidOperationException($"Cannot reactivate identifier: value '{identifier.IdentifierValue}' is currently in use by another active location.");
        }

        if (isPrimary)
        {
            await DemoteExistingPrimariesAsync(tenantId, locationId);
        }

        identifier.Reactivate(isPrimary);
        await _context.SaveChangesAsync();

        return MapToDto(identifier);
    }

    private async Task ValidateLocationScopeAsync(Guid tenantId, Guid warehouseId, Guid locationId)
    {
        var location = await _context.WarehouseLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == locationId)
            ?? throw new KeyNotFoundException($"Warehouse location '{locationId}' not found in the current tenant scope.");

        if (location.WarehouseId != warehouseId)
        {
            throw new InvalidOperationException($"Location '{locationId}' belongs to warehouse '{location.WarehouseId}', not '{warehouseId}'. Cross-warehouse operation rejected.");
        }
    }

    private async Task DemoteExistingPrimariesAsync(Guid tenantId, Guid locationId)
    {
        var existingPrimaries = await _context.LocationIdentifiers
            .Where(li => li.TenantId == tenantId &&
                         li.WarehouseLocationId == locationId &&
                         li.IsPrimary &&
                         li.IsActive)
            .ToListAsync();

        foreach (var primary in existingPrimaries)
        {
            primary.DemoteFromPrimary();
        }
    }

    private static string GenerateCandidateCode(Guid tenantId, Guid locationId, IdentifierSymbology symbology)
    {
        if (symbology == IdentifierSymbology.QRCode)
        {
            return $"urn:apex:v1:loc:{tenantId:N}:{locationId:N}";
        }

        // Code 128 internal candidate: APX-L-YYMMDD-XXXXX-C
        byte[] randomBytes = new byte[4];
        RandomNumberGenerator.Fill(randomBytes);
        string hex = BitConverter.ToString(randomBytes).Replace("-", "").Substring(0, 5).ToUpperInvariant();
        string baseStr = $"APX-L-{DateTime.UtcNow:yyMMdd}-{hex}";

        int check = Math.Abs(baseStr.GetHashCode()) % 10;
        return $"{baseStr}-{check}";
    }

    private static LocationIdentifierDto MapToDto(LocationIdentifier entity)
    {
        return new LocationIdentifierDto
        {
            LocationIdentifierId = entity.LocationIdentifierId,
            TenantId = entity.TenantId,
            WarehouseId = entity.WarehouseId,
            WarehouseLocationId = entity.WarehouseLocationId,
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
