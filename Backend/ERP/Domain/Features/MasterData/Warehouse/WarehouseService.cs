using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _repository;
    private readonly IAddressRepository _addressRepository;
    private readonly DomainDbContext _context;

    public WarehouseService(
        IWarehouseRepository repository,
        IAddressRepository addressRepository,
        DomainDbContext context)
    {
        _repository = repository;
        _addressRepository = addressRepository;
        _context = context;
    }

    public async Task<List<Warehouse>> GetWarehousesAsync(Guid tenantId)
    {
        return await _repository.GetWarehousesAsync(tenantId);
    }

    public async Task<Warehouse?> GetWarehouseAsync(Guid tenantId, Guid warehouseId)
    {
        return await _repository.GetWarehouseByIdAsync(tenantId, warehouseId);
    }

    public async Task<Warehouse> CreateWarehouseAsync(Guid tenantId, CreateWarehouseRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
            throw new ArgumentException("Warehouse code is required.", nameof(request.WarehouseCode));

        if (string.IsNullOrWhiteSpace(request.WarehouseName))
            throw new ArgumentException("Warehouse name is required.", nameof(request.WarehouseName));

        string code = request.WarehouseCode.Trim();
        string name = request.WarehouseName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Warehouse code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Warehouse name '{name}' already exists for this tenant.");

        // Tenant-scoped Company & Branch validation
        if (request.CompanyId.HasValue)
        {
            if (!await _repository.CompanyBelongsToTenantAsync(tenantId, request.CompanyId.Value))
                throw new InvalidOperationException("Company not found or does not belong to the current tenant.");

            if (request.BranchId.HasValue)
            {
                if (!await _repository.BranchBelongsToCompanyAndTenantAsync(tenantId, request.CompanyId.Value, request.BranchId.Value))
                    throw new InvalidOperationException("Branch not found or does not belong to the specified Company and tenant.");
            }
        }
        else if (request.BranchId.HasValue)
        {
            throw new InvalidOperationException("Cannot assign Branch without assigning a valid Company.");
        }

        var warehouse = Warehouse.Create(
            tenantId,
            code,
            name,
            request.Description?.Trim(),
            request.CompanyId,
            request.BranchId,
            request.AddressLine1?.Trim(),
            request.AddressLine2?.Trim(),
            request.City?.Trim(),
            request.PostalCode?.Trim(),
            request.Country?.Trim());

        await _repository.AddWarehouseAsync(warehouse);
        await _repository.SaveChangesAsync();
        return warehouse;
    }

    public async Task<Warehouse> UpdateWarehouseAsync(Guid tenantId, Guid warehouseId, UpdateWarehouseRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var warehouse = await _repository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found.");

        if (string.IsNullOrWhiteSpace(request.WarehouseName))
            throw new ArgumentException("Warehouse name is required.", nameof(request.WarehouseName));

        string name = request.WarehouseName.Trim();
        if (warehouse.WarehouseName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Warehouse name '{name}' already exists for this tenant.");

        if (request.CompanyId.HasValue)
        {
            if (!await _repository.CompanyBelongsToTenantAsync(tenantId, request.CompanyId.Value))
                throw new InvalidOperationException("Company not found or does not belong to the current tenant.");

            if (request.BranchId.HasValue)
            {
                if (!await _repository.BranchBelongsToCompanyAndTenantAsync(tenantId, request.CompanyId.Value, request.BranchId.Value))
                    throw new InvalidOperationException("Branch not found or does not belong to the specified Company and tenant.");
            }
        }
        else if (request.BranchId.HasValue)
        {
            throw new InvalidOperationException("Cannot assign Branch without assigning a valid Company.");
        }

        warehouse.Update(
            name,
            request.Description?.Trim(),
            request.CompanyId,
            request.BranchId,
            request.AddressLine1?.Trim(),
            request.AddressLine2?.Trim(),
            request.City?.Trim(),
            request.PostalCode?.Trim(),
            request.Country?.Trim());

        await _repository.SaveChangesAsync();
        return warehouse;
    }

    public async Task DeactivateWarehouseAsync(Guid tenantId, Guid warehouseId)
    {
        var warehouse = await _repository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found.");

        var activeZones = (await _repository.GetZonesAsync(tenantId, warehouseId))
            .Where(x => x.IsActive)
            .ToList();
        if (activeZones.Any())
        {
            throw new InvalidOperationException("Cannot deactivate a warehouse that currently has active zones.");
        }

        var activeLocations = (await _repository.GetLocationsAsync(tenantId, warehouseId))
            .Where(x => x.IsActive)
            .ToList();
        if (activeLocations.Any())
        {
            throw new InvalidOperationException("Cannot deactivate a warehouse that currently has active storage locations.");
        }

        warehouse.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<WarehouseZone>> GetZonesAsync(Guid tenantId, Guid warehouseId)
    {
        return await _repository.GetZonesAsync(tenantId, warehouseId);
    }

    public async Task<WarehouseZone?> GetZoneAsync(Guid tenantId, Guid zoneId)
    {
        return await _repository.GetZoneByIdAsync(tenantId, zoneId);
    }

    public async Task<WarehouseZone> CreateZoneAsync(Guid tenantId, Guid warehouseId, CreateWarehouseZoneRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var warehouse = await _repository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found.");

        if (string.IsNullOrWhiteSpace(request.ZoneCode))
            throw new ArgumentException("Zone code is required.", nameof(request.ZoneCode));

        if (string.IsNullOrWhiteSpace(request.ZoneName))
            throw new ArgumentException("Zone name is required.", nameof(request.ZoneName));

        string code = request.ZoneCode.Trim();
        if (await _repository.ZoneExistsByCodeAsync(tenantId, warehouseId, code))
            throw new InvalidOperationException($"Zone code '{code}' already exists in this warehouse.");

        var zone = WarehouseZone.Create(
            tenantId,
            warehouseId,
            code,
            request.ZoneName.Trim(),
            request.Description?.Trim());

        await _repository.AddZoneAsync(zone);
        await _repository.SaveChangesAsync();
        return zone;
    }

    public async Task<WarehouseZone> UpdateZoneAsync(Guid tenantId, Guid zoneId, UpdateWarehouseZoneRequest request, Guid? warehouseId = null)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var zone = await _repository.GetZoneByIdAsync(tenantId, zoneId)
            ?? throw new KeyNotFoundException("Warehouse zone not found.");

        if (warehouseId.HasValue && zone.WarehouseId != warehouseId.Value)
            throw new InvalidOperationException("The specified zone does not belong to the given warehouse.");

        if (string.IsNullOrWhiteSpace(request.ZoneName))
            throw new ArgumentException("Zone name is required.", nameof(request.ZoneName));

        zone.Update(request.ZoneName.Trim(), request.Description?.Trim());
        await _repository.SaveChangesAsync();
        return zone;
    }

    public async Task DeactivateZoneAsync(Guid tenantId, Guid zoneId, Guid? warehouseId = null)
    {
        var zone = await _repository.GetZoneByIdAsync(tenantId, zoneId)
            ?? throw new KeyNotFoundException("Warehouse zone not found.");

        if (warehouseId.HasValue && zone.WarehouseId != warehouseId.Value)
            throw new InvalidOperationException("The specified zone does not belong to the given warehouse.");

        var activeLocations = (await _repository.GetLocationsAsync(tenantId, zone.WarehouseId, zoneId))
            .Where(x => x.IsActive)
            .ToList();
        if (activeLocations.Any())
        {
            throw new InvalidOperationException("Cannot deactivate a warehouse zone that has active storage locations assigned to it.");
        }

        zone.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<WarehouseLocation>> GetLocationsAsync(Guid tenantId, Guid warehouseId, Guid? zoneId = null)
    {
        return await _repository.GetLocationsAsync(tenantId, warehouseId, zoneId);
    }

    public async Task<List<WarehouseLocationDetailDto>> GetLocationsWithDetailsAsync(Guid tenantId, Guid warehouseId, Guid? zoneId = null)
    {
        var locations = await _repository.GetLocationsAsync(tenantId, warehouseId, zoneId);
        var zones = await _repository.GetZonesAsync(tenantId, warehouseId);
        var types = await _repository.GetLocationTypesAsync(tenantId);

        var zoneDict = zones.ToDictionary(z => z.WarehouseZoneId);
        var typeDict = types.ToDictionary(t => t.LocationTypeId);
        var locDict = locations.ToDictionary(l => l.WarehouseLocationId);

        var result = new List<WarehouseLocationDetailDto>(locations.Count);

        foreach (var loc in locations)
        {
            var dto = new WarehouseLocationDetailDto
            {
                WarehouseLocationId = loc.WarehouseLocationId,
                TenantId = loc.TenantId,
                WarehouseId = loc.WarehouseId,
                WarehouseZoneId = loc.WarehouseZoneId,
                LocationTypeId = loc.LocationTypeId,
                ParentLocationId = loc.ParentLocationId,
                LocationCode = loc.LocationCode,
                LocationName = loc.LocationName,
                IsActive = loc.IsActive
            };

            if (loc.WarehouseZoneId.HasValue && zoneDict.TryGetValue(loc.WarehouseZoneId.Value, out var z))
            {
                dto.ZoneCode = z.ZoneCode;
                dto.ZoneName = z.ZoneName;
            }

            if (loc.ParentLocationId.HasValue && locDict.TryGetValue(loc.ParentLocationId.Value, out var p))
            {
                dto.ParentLocationCode = p.LocationCode;
                dto.ParentLocationName = p.LocationName;
            }

            if (loc.LocationTypeId.HasValue && typeDict.TryGetValue(loc.LocationTypeId.Value, out var t))
            {
                dto.LocationTypeCode = t.Code;
                dto.LocationTypeName = t.Name;
                dto.CanStoreInventory = t.CanStoreInventory;
                dto.CanPick = t.CanPick;
                dto.CanPutAway = t.CanPutAway;
                dto.CanContainChildren = t.CanContainChildren;
            }

            var pathParts = new List<string>();
            if (!string.IsNullOrEmpty(dto.ZoneName))
            {
                pathParts.Add(dto.ZoneName);
            }

            var ancestorCodes = new List<string>();
            var currParentId = loc.ParentLocationId;
            var visited = new HashSet<Guid> { loc.WarehouseLocationId };
            while (currParentId.HasValue && locDict.TryGetValue(currParentId.Value, out var parentNode))
            {
                if (!visited.Add(currParentId.Value)) break;
                ancestorCodes.Insert(0, parentNode.LocationCode);
                currParentId = parentNode.ParentLocationId;
            }

            pathParts.AddRange(ancestorCodes);
            pathParts.Add(loc.LocationCode);
            dto.HierarchyPath = string.Join(" / ", pathParts);

            result.Add(dto);
        }

        return result;
    }

    public async Task<WarehouseLocation?> GetLocationAsync(Guid tenantId, Guid locationId)
    {
        return await _repository.GetLocationByIdAsync(tenantId, locationId);
    }

    public async Task<WarehouseLocation> CreateLocationAsync(Guid tenantId, Guid warehouseId, Guid? zoneId, CreateWarehouseLocationRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var warehouse = await _repository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found.");

        var effectiveZoneId = zoneId ?? request.WarehouseZoneId;

        if (effectiveZoneId.HasValue)
        {
            var zone = await _repository.GetZoneByIdAsync(tenantId, effectiveZoneId.Value)
                ?? throw new KeyNotFoundException("Warehouse zone not found.");

            if (zone.WarehouseId != warehouseId)
                throw new InvalidOperationException("Specified zone does not belong to this warehouse.");
        }

        if (string.IsNullOrWhiteSpace(request.LocationCode))
            throw new ArgumentException("Location code is required.", nameof(request.LocationCode));

        if (string.IsNullOrWhiteSpace(request.LocationName))
            throw new ArgumentException("Location name is required.", nameof(request.LocationName));

        string code = request.LocationCode.Trim();
        if (await _repository.LocationExistsByCodeAsync(tenantId, warehouseId, code))
            throw new InvalidOperationException($"Location code '{code}' already exists in this warehouse.");

        LocationType? childLocType = null;
        if (request.LocationTypeId.HasValue)
        {
            childLocType = await _repository.GetLocationTypeByIdAsync(tenantId, request.LocationTypeId.Value);
            if (childLocType == null)
                throw new InvalidOperationException("Location type not found or does not belong to the current tenant.");
        }

        if (request.ParentLocationId.HasValue)
        {
            var parent = await _repository.GetLocationByIdAsync(tenantId, request.ParentLocationId.Value);
            if (parent == null || parent.WarehouseId != warehouseId)
                throw new InvalidOperationException("Parent location must belong to the exact same tenant and warehouse.");

            if (!parent.IsActive)
                throw new InvalidOperationException("Parent location must be active.");

            if (effectiveZoneId.HasValue && parent.WarehouseZoneId.HasValue && effectiveZoneId.Value != parent.WarehouseZoneId.Value)
                throw new InvalidOperationException("Parent location must belong to the same zone if both locations have zones assigned.");

            LocationType? parentLocType = null;
            if (parent.LocationTypeId.HasValue)
            {
                parentLocType = await _repository.GetLocationTypeByIdAsync(tenantId, parent.LocationTypeId.Value);
                if (parentLocType != null && !parentLocType.CanContainChildren)
                    throw new InvalidOperationException($"Parent location '{parent.LocationCode}' cannot contain child locations.");
            }

            ValidateHierarchyTypeRelationship(childLocType, parentLocType, parent.LocationCode);
        }

        var location = WarehouseLocation.Create(
            tenantId,
            warehouseId,
            effectiveZoneId,
            code,
            request.LocationName.Trim(),
            request.LocationTypeId,
            request.ParentLocationId);

        await _repository.AddLocationAsync(location);
        await _repository.SaveChangesAsync();
        return location;
    }

    public async Task<WarehouseLocation> UpdateLocationAsync(Guid tenantId, Guid locationId, UpdateWarehouseLocationRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var location = await _repository.GetLocationByIdAsync(tenantId, locationId)
            ?? throw new KeyNotFoundException("Warehouse location not found.");

        if (string.IsNullOrWhiteSpace(request.LocationName))
            throw new ArgumentException("Location name is required.", nameof(request.LocationName));

        LocationType? childLocType = null;
        if (request.LocationTypeId.HasValue)
        {
            childLocType = await _repository.GetLocationTypeByIdAsync(tenantId, request.LocationTypeId.Value);
            if (childLocType == null)
                throw new InvalidOperationException("Location type not found or does not belong to the current tenant.");

            if (!childLocType.CanContainChildren)
            {
                var existingChildren = (await _repository.GetLocationsAsync(tenantId, location.WarehouseId))
                    .Where(x => x.ParentLocationId == locationId && x.IsActive)
                    .ToList();
                if (existingChildren.Any())
                    throw new InvalidOperationException($"Cannot change location type to '{childLocType.Name}' because this location currently has active child locations.");
            }
        }

        if (request.ParentLocationId.HasValue)
        {
            if (request.ParentLocationId.Value == locationId)
                throw new InvalidOperationException("A location cannot be its own parent.");

            var parent = await _repository.GetLocationByIdAsync(tenantId, request.ParentLocationId.Value);
            if (parent == null || parent.WarehouseId != location.WarehouseId)
                throw new InvalidOperationException("Parent location must belong to the exact same tenant and warehouse.");

            if (!parent.IsActive)
                throw new InvalidOperationException("Parent location must be active.");

            if (location.WarehouseZoneId.HasValue && parent.WarehouseZoneId.HasValue && location.WarehouseZoneId.Value != parent.WarehouseZoneId.Value)
                throw new InvalidOperationException("Parent location must belong to the same zone if both locations have zones assigned.");

            LocationType? parentLocType = null;
            if (parent.LocationTypeId.HasValue)
            {
                parentLocType = await _repository.GetLocationTypeByIdAsync(tenantId, parent.LocationTypeId.Value);
                if (parentLocType != null && !parentLocType.CanContainChildren)
                    throw new InvalidOperationException($"Parent location '{parent.LocationCode}' cannot contain child locations.");
            }

            ValidateHierarchyTypeRelationship(childLocType, parentLocType, parent.LocationCode);

            // Recursive cycle detection
            var currentParentId = parent.ParentLocationId;
            var visited = new HashSet<Guid> { locationId, parent.WarehouseLocationId };
            while (currentParentId.HasValue)
            {
                if (visited.Contains(currentParentId.Value))
                    throw new InvalidOperationException("Circular parent location hierarchy detected.");
                visited.Add(currentParentId.Value);
                var ancestor = await _repository.GetLocationByIdAsync(tenantId, currentParentId.Value);
                currentParentId = ancestor?.ParentLocationId;
            }
        }

        location.Update(
            request.LocationName.Trim(),
            request.LocationTypeId,
            request.ParentLocationId);

        await _repository.SaveChangesAsync();
        return location;
    }

    public async Task DeactivateLocationAsync(Guid tenantId, Guid locationId)
    {
        var location = await _repository.GetLocationByIdAsync(tenantId, locationId)
            ?? throw new KeyNotFoundException("Warehouse location not found.");

        // Safe structural change safeguard: Check if location has child locations before deactivating
        var childLocations = (await _repository.GetLocationsAsync(tenantId, location.WarehouseId))
            .Where(x => x.ParentLocationId == locationId && x.IsActive)
            .ToList();
        if (childLocations.Any())
        {
            throw new InvalidOperationException("Cannot deactivate a location that currently has active child locations.");
        }

        location.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<LocationType>> GetLocationTypesAsync(Guid tenantId)
    {
        return await _repository.GetLocationTypesAsync(tenantId);
    }

    public async Task<LocationType> CreateLocationTypeAsync(Guid tenantId, CreateLocationTypeRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Location type code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Location type name is required.", nameof(request.Name));

        string code = request.Code.Trim().ToUpperInvariant();
        var existing = await _repository.GetLocationTypeByCodeAsync(tenantId, code);
        if (existing != null)
            throw new InvalidOperationException($"Location type '{code}' already exists.");

        var locType = LocationType.Create(
            tenantId,
            code,
            request.Name.Trim(),
            request.Description?.Trim(),
            request.CanStoreInventory,
            request.CanPick,
            request.CanPutAway,
            request.CanContainChildren);

        await _repository.AddLocationTypeAsync(locType);
        await _repository.SaveChangesAsync();
        return locType;
    }

    public async Task<List<WarehouseAddress>> GetAddressesAsync(Guid tenantId, Guid warehouseId)
    {
        return await _repository.GetAddressesAsync(tenantId, warehouseId);
    }

    public async Task<WarehouseAddress> LinkAddressAsync(Guid tenantId, Guid warehouseId, WarehouseAddressLinkRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var warehouse = await _repository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found.");

        var address = await _addressRepository.GetAddressByIdAsync(tenantId, request.AddressId);
        if (address == null)
            throw new InvalidOperationException("Address not found or does not belong to the current tenant.");

        var addressType = await _addressRepository.GetAddressTypeByIdAsync(tenantId, request.AddressTypeId);
        if (addressType == null)
            throw new InvalidOperationException("AddressType not found or does not belong to the current tenant.");

        var existing = await _repository.GetAddressAssociationAsync(tenantId, warehouseId, request.AddressId, request.AddressTypeId);
        if (existing != null)
            throw new InvalidOperationException("This address association already exists for this warehouse.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        if (request.IsDefault)
        {
            var activeDefaults = (await _repository.GetAddressesAsync(tenantId, warehouseId))
                .Where(x => x.AddressTypeId == request.AddressTypeId && x.IsDefault && x.IsActive)
                .ToList();

            foreach (var d in activeDefaults)
            {
                d.SetAsDefault(false);
            }
            await _repository.SaveChangesAsync();
        }

        var association = WarehouseAddress.Create(
            tenantId,
            warehouseId,
            request.AddressId,
            request.AddressTypeId,
            request.IsDefault);

        await _repository.AddAddressAssociationAsync(association);
        await _repository.SaveChangesAsync();

        if (request.IsDefault)
        {
            string? countryName = null;
            if (address.CountryId.HasValue)
            {
                var country = await _addressRepository.GetCountryByIdAsync(address.CountryId.Value);
                countryName = country?.CountryName;
            }
            else
            {
                countryName = address.UnmatchedCountryText;
            }

            warehouse.SyncDefaultAddress(address.AddressLine1, address.AddressLine2, address.City, address.PostalCode, countryName);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return association;
    }

    public async Task SetDefaultAddressAsync(Guid tenantId, Guid warehouseId, Guid warehouseAddressId)
    {
        var warehouse = await _repository.GetWarehouseByIdAsync(tenantId, warehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found.");

        var association = await _repository.GetAddressAssociationByIdAsync(tenantId, warehouseAddressId)
            ?? throw new KeyNotFoundException("Warehouse address association not found.");

        if (association.WarehouseId != warehouseId)
            throw new InvalidOperationException("Warehouse address does not belong to this warehouse.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var activeDefaults = (await _repository.GetAddressesAsync(tenantId, warehouseId))
            .Where(x => x.AddressTypeId == association.AddressTypeId && x.IsDefault && x.IsActive)
            .ToList();

        foreach (var d in activeDefaults)
        {
            d.SetAsDefault(false);
        }
        await _repository.SaveChangesAsync();

        association.SetAsDefault(true);
        await _repository.SaveChangesAsync();

        var address = await _addressRepository.GetAddressByIdAsync(tenantId, association.AddressId);
        if (address != null)
        {
            string? countryName = null;
            if (address.CountryId.HasValue)
            {
                var country = await _addressRepository.GetCountryByIdAsync(address.CountryId.Value);
                countryName = country?.CountryName;
            }
            else
            {
                countryName = address.UnmatchedCountryText;
            }

            warehouse.SyncDefaultAddress(address.AddressLine1, address.AddressLine2, address.City, address.PostalCode, countryName);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task RemoveAddressLinkAsync(Guid tenantId, Guid warehouseId, Guid warehouseAddressId)
    {
        var association = await _repository.GetAddressAssociationByIdAsync(tenantId, warehouseAddressId)
            ?? throw new KeyNotFoundException("Warehouse address association not found.");

        if (association.WarehouseId != warehouseId)
            throw new InvalidOperationException("Warehouse address does not belong to this warehouse.");

        association.Deactivate();
        await _repository.SaveChangesAsync();
    }

    private static void ValidateHierarchyTypeRelationship(LocationType? childType, LocationType? parentType, string parentCode)
    {
        if (childType == null || parentType == null) return;

        string childCode = childType.Code.Trim().ToUpperInvariant();
        string pCode = parentType.Code.Trim().ToUpperInvariant();

        // Rule 1: A BIN cannot have a RACK parent directly
        if (childCode == "BIN" && pCode == "RACK")
        {
            throw new InvalidOperationException($"A bin location cannot be placed directly under a rack ('{parentCode}'). It must be placed under a shelf.");
        }

        // Rule 2: A BIN cannot have a BIN parent
        if (childCode == "BIN" && pCode == "BIN")
        {
            throw new InvalidOperationException($"A bin location cannot be placed under another bin ('{parentCode}').");
        }

        // Rule 3: A SHELF cannot have a BIN parent
        if (childCode == "SHELF" && pCode == "BIN")
        {
            throw new InvalidOperationException($"A shelf location cannot be placed under a bin ('{parentCode}').");
        }

        // Rule 4: A SHELF cannot have a SHELF parent
        if (childCode == "SHELF" && pCode == "SHELF")
        {
            throw new InvalidOperationException($"A shelf location cannot be placed under another shelf ('{parentCode}').");
        }

        // Rule 5: A RACK cannot be placed under a SHELF or BIN
        if (childCode == "RACK" && (pCode == "SHELF" || pCode == "BIN"))
        {
            throw new InvalidOperationException($"A rack location cannot be placed under a {pCode.ToLowerInvariant()} ('{parentCode}'). Racks are top-level storage locations.");
        }

        // Rule 6: FLOOR / STAGING cannot be placed under SHELF or BIN
        if ((childCode == "FLOOR" || childCode == "STAGING") && (pCode == "SHELF" || pCode == "BIN"))
        {
            throw new InvalidOperationException($"A {childCode.ToLowerInvariant()} location cannot be placed under a {pCode.ToLowerInvariant()} ('{parentCode}').");
        }
    }
}
