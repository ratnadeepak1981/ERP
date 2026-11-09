using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Domain.Features.MasterData.Common;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.MasterData.Warehouse;

public class LocationIdentifierService : ILocationIdentifierService
{
    private readonly ILocationIdentifierRepository _repository;
    private readonly ILogger<LocationIdentifierService>? _logger;

    public LocationIdentifierService(
        ILocationIdentifierRepository repository,
        ILogger<LocationIdentifierService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
    }

    public async Task<List<LocationIdentifierDto>> GetIdentifiersAsync(Guid tenantId, Guid warehouseId, Guid locationId, bool includeInactive = false)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var list = await _repository.GetIdentifiersAsync(tenantId, locationId, includeInactive);
        return list.Select(MapToDto).ToList();
    }

    public async Task<LocationIdentifierDto?> GetIdentifierByIdAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, locationId, identifierId);

        return identifier == null ? null : MapToDto(identifier);
    }

    public async Task<LocationIdentifierDto> AddIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, CreateLocationIdentifierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        string normalized = request.IdentifierValue.Trim().ToUpperInvariant();
        SymbologyValidator.Validate(request.Symbology, normalized);

        // Value uniqueness check for active identifiers in this tenant
        bool exists = await _repository.ExistsActiveValueAsync(tenantId, normalized);
        if (exists)
        {
            throw new InvalidOperationException($"Location identifier value '{normalized}' is already registered to an active location in this tenant.");
        }

        if (request.IsPrimary)
        {
            await _repository.DemoteExistingPrimariesAsync(tenantId, locationId);
            await _repository.SaveChangesAsync();
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

        await _repository.AddIdentifierAsync(identifier);
        await _repository.SaveChangesAsync();

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

            bool exists = await _repository.ExistsActiveValueAsync(tenantId, candidate);
            if (exists)
            {
                _logger?.LogWarning("Candidate collision detected for location '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                continue;
            }

            if (request.IsPrimary)
            {
                await _repository.DemoteExistingPrimariesAsync(tenantId, locationId);
                await _repository.SaveChangesAsync();
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
                await _repository.AddIdentifierAsync(identifier);
                await _repository.SaveChangesAsync();
                return MapToDto(identifier);
            }
            catch (DbUpdateException ex) when (attempt < maxRetries)
            {
                _logger?.LogWarning(ex, "DbUpdateException on location candidate '{Candidate}'. Retrying attempt {Attempt}/{Max}...", candidate, attempt, maxRetries);
                _repository.Detach(identifier);
            }
        }

        throw new InvalidOperationException("Failed to generate a unique internal location identifier after multiple attempts due to uniqueness collision.");
    }

    public async Task<LocationIdentifierDto> SetPrimaryAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, locationId, identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for location '{locationId}'.");

        if (!identifier.IsActive)
        {
            throw new InvalidOperationException("Cannot set an inactive location identifier as primary.");
        }

        if (identifier.IsPrimary)
        {
            return MapToDto(identifier);
        }

        await _repository.DemoteExistingPrimariesAsync(tenantId, locationId);
        await _repository.SaveChangesAsync();

        identifier.PromoteToPrimary();
        await _repository.SaveChangesAsync();

        return MapToDto(identifier);
    }

    public async Task DeactivateIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, locationId, identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for location '{locationId}'.");

        identifier.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<LocationIdentifierDto> ReactivateIdentifierAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid identifierId, bool isPrimary = false)
    {
        await ValidateLocationScopeAsync(tenantId, warehouseId, locationId);

        var identifier = await _repository.GetIdentifierByIdAsync(tenantId, locationId, identifierId)
            ?? throw new KeyNotFoundException($"Identifier '{identifierId}' not found for location '{locationId}'.");

        if (identifier.IsActive)
        {
            return MapToDto(identifier);
        }

        bool exists = await _repository.ExistsActiveValueAsync(tenantId, identifier.IdentifierValue);
        if (exists)
        {
            throw new InvalidOperationException($"Cannot reactivate identifier: value '{identifier.IdentifierValue}' is currently in use by another active location.");
        }

        if (isPrimary)
        {
            await _repository.DemoteExistingPrimariesAsync(tenantId, locationId);
        }

        identifier.Reactivate(isPrimary);
        await _repository.SaveChangesAsync();

        return MapToDto(identifier);
    }

    private async Task ValidateLocationScopeAsync(Guid tenantId, Guid warehouseId, Guid locationId)
    {
        var location = await _repository.GetLocationScopeAsync(tenantId, locationId)
            ?? throw new KeyNotFoundException($"Warehouse location '{locationId}' not found in the current tenant scope.");

        if (location.WarehouseId != warehouseId)
        {
            throw new InvalidOperationException($"Location '{locationId}' belongs to warehouse '{location.WarehouseId}', not '{warehouseId}'. Cross-warehouse operation rejected.");
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
