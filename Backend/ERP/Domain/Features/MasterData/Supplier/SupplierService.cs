using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Supplier;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repository;
    private readonly IAddressRepository _addressRepository;
    private readonly DomainDbContext _context;

    public SupplierService(
        ISupplierRepository repository,
        IAddressRepository addressRepository,
        DomainDbContext context)
    {
        _repository = repository;
        _addressRepository = addressRepository;
        _context = context;
    }

    public async Task<List<Supplier>> GetSuppliersAsync(Guid tenantId)
    {
        return await _repository.GetSuppliersAsync(tenantId);
    }

    public async Task<Supplier?> GetSupplierAsync(Guid tenantId, Guid supplierId)
    {
        return await _repository.GetSupplierByIdAsync(tenantId, supplierId);
    }

    public async Task<Supplier> CreateSupplierAsync(Guid tenantId, CreateSupplierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.SupplierCode))
            throw new ArgumentException("Supplier code is required.", nameof(request.SupplierCode));

        if (string.IsNullOrWhiteSpace(request.SupplierName))
            throw new ArgumentException("Supplier name is required.", nameof(request.SupplierName));

        string code = request.SupplierCode.Trim();
        string name = request.SupplierName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Supplier code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Supplier name '{name}' already exists for this tenant.");

        var supplier = Supplier.Create(
            tenantId,
            code,
            name,
            request.ContactPerson?.Trim(),
            request.Email?.Trim(),
            request.Phone?.Trim(),
            request.AddressLine1?.Trim(),
            request.AddressLine2?.Trim(),
            request.City?.Trim(),
            request.PostalCode?.Trim(),
            request.Country?.Trim(),
            request.TaxNumber?.Trim(),
            request.CreditTerms?.Trim(),
            request.PaymentTerms?.Trim());

        await _repository.AddSupplierAsync(supplier);
        await _repository.SaveChangesAsync();

        // If legacy address fields were provided, create normalized address and link as default
        if (!string.IsNullOrWhiteSpace(request.AddressLine1) && !string.IsNullOrWhiteSpace(request.City))
        {
            await CreateNormalizedAddressForSupplierAsync(tenantId, supplier, request);
        }

        // If legacy contact fields were provided, create normalized contact and link as primary
        if (!string.IsNullOrWhiteSpace(request.ContactPerson) || !string.IsNullOrWhiteSpace(request.Email) || !string.IsNullOrWhiteSpace(request.Phone))
        {
            await CreateNormalizedContactForSupplierAsync(tenantId, supplier, request);
        }

        return supplier;
    }

    private async Task CreateNormalizedAddressForSupplierAsync(Guid tenantId, Supplier supplier, CreateSupplierRequest request)
    {
        Guid? countryId = null;
        string? unmatchedCountry = request.Country?.Trim();

        if (!string.IsNullOrWhiteSpace(unmatchedCountry))
        {
            var country = await _addressRepository.GetCountryByCodeOrNameAsync(unmatchedCountry);
            if (country != null)
            {
                countryId = country.CountryId;
                unmatchedCountry = null;
            }
        }

        var address = Address.Create(
            tenantId,
            $"{supplier.SupplierName} - Default Address",
            request.AddressLine1!.Trim(),
            request.City.Trim(),
            request.AddressLine2?.Trim(),
            null,
            request.PostalCode?.Trim(),
            countryId,
            unmatchedCountry);

        await _addressRepository.AddAddressAsync(address);
        await _addressRepository.SaveChangesAsync();

        var addressType = await _addressRepository.GetAddressTypeByCodeAsync(tenantId, "GENERAL")
            ?? await _addressRepository.GetAddressTypeByCodeAsync(tenantId, "OTHER")
            ?? (await _addressRepository.GetAddressTypesAsync(tenantId)).FirstOrDefault();

        if (addressType != null)
        {
            var association = SupplierAddress.Create(tenantId, supplier.SupplierId, address.AddressId, addressType.AddressTypeId, true);
            await _repository.AddAddressAssociationAsync(association);
            await _repository.SaveChangesAsync();
        }
    }

    private async Task CreateNormalizedContactForSupplierAsync(Guid tenantId, Supplier supplier, CreateSupplierRequest request)
    {
        string contactName = !string.IsNullOrWhiteSpace(request.ContactPerson) ? request.ContactPerson.Trim() : $"{supplier.SupplierName} Contact";
        var contact = global::Domain.Features.MasterData.Contact.Contact.Create(
            tenantId,
            contactName,
            null,
            request.Email?.Trim(),
            request.Phone?.Trim());

        await _addressRepository.AddContactAsync(contact);
        await _addressRepository.SaveChangesAsync();

        var contactType = await _addressRepository.GetContactTypeByCodeAsync(tenantId, "PRIMARY")
            ?? await _addressRepository.GetContactTypeByCodeAsync(tenantId, "GENERAL")
            ?? (await _addressRepository.GetContactTypesAsync(tenantId)).FirstOrDefault();

        if (contactType != null)
        {
            var association = SupplierContact.Create(tenantId, supplier.SupplierId, contact.ContactId, contactType.ContactTypeId, true);
            await _repository.AddContactAssociationAsync(association);
            await _repository.SaveChangesAsync();
        }
    }

    public async Task<Supplier> UpdateSupplierAsync(Guid tenantId, Guid supplierId, UpdateSupplierRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var supplier = await _repository.GetSupplierByIdAsync(tenantId, supplierId)
            ?? throw new KeyNotFoundException("Supplier not found.");

        if (string.IsNullOrWhiteSpace(request.SupplierName))
            throw new ArgumentException("Supplier name is required.", nameof(request.SupplierName));

        string name = request.SupplierName.Trim();
        if (supplier.SupplierName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Supplier name '{name}' already exists for this tenant.");

        supplier.Update(
            name,
            request.ContactPerson?.Trim(),
            request.Email?.Trim(),
            request.Phone?.Trim(),
            request.AddressLine1?.Trim(),
            request.AddressLine2?.Trim(),
            request.City?.Trim(),
            request.PostalCode?.Trim(),
            request.Country?.Trim(),
            request.TaxNumber?.Trim(),
            request.CreditTerms?.Trim(),
            request.PaymentTerms?.Trim());

        await _repository.SaveChangesAsync();
        return supplier;
    }

    public async Task DeactivateSupplierAsync(Guid tenantId, Guid supplierId)
    {
        var supplier = await _repository.GetSupplierByIdAsync(tenantId, supplierId)
            ?? throw new KeyNotFoundException("Supplier not found.");

        supplier.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<SupplierAddress>> GetAddressesAsync(Guid tenantId, Guid supplierId)
    {
        return await _repository.GetAddressesAsync(tenantId, supplierId);
    }

    public async Task<SupplierAddress> LinkAddressAsync(Guid tenantId, Guid supplierId, SupplierAddressLinkRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var supplier = await _repository.GetSupplierByIdAsync(tenantId, supplierId)
            ?? throw new KeyNotFoundException("Supplier not found.");

        var address = await _addressRepository.GetAddressByIdAsync(tenantId, request.AddressId);
        if (address == null)
            throw new InvalidOperationException("Address not found or does not belong to the current tenant.");

        var addressType = await _addressRepository.GetAddressTypeByIdAsync(tenantId, request.AddressTypeId);
        if (addressType == null)
            throw new InvalidOperationException("AddressType not found or does not belong to the current tenant.");

        var existing = await _repository.GetAddressAssociationAsync(tenantId, supplierId, request.AddressId, request.AddressTypeId);
        if (existing != null)
            throw new InvalidOperationException("This address association already exists for this supplier.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        if (request.IsDefault)
        {
            var activeDefaults = (await _repository.GetAddressesAsync(tenantId, supplierId))
                .Where(x => x.AddressTypeId == request.AddressTypeId && x.IsDefault && x.IsActive)
                .ToList();

            foreach (var d in activeDefaults)
            {
                d.SetAsDefault(false);
            }
            await _repository.SaveChangesAsync();
        }

        var association = SupplierAddress.Create(
            tenantId,
            supplierId,
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

            supplier.SyncDefaultAddress(address.AddressLine1, address.AddressLine2, address.City, address.PostalCode, countryName);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return association;
    }

    public async Task SetDefaultAddressAsync(Guid tenantId, Guid supplierId, Guid supplierAddressId)
    {
        var supplier = await _repository.GetSupplierByIdAsync(tenantId, supplierId)
            ?? throw new KeyNotFoundException("Supplier not found.");

        var association = await _repository.GetAddressAssociationByIdAsync(tenantId, supplierAddressId)
            ?? throw new KeyNotFoundException("Supplier address association not found.");

        if (association.SupplierId != supplierId)
            throw new InvalidOperationException("Supplier address does not belong to this supplier.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var activeDefaults = (await _repository.GetAddressesAsync(tenantId, supplierId))
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

            supplier.SyncDefaultAddress(address.AddressLine1, address.AddressLine2, address.City, address.PostalCode, countryName);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task RemoveAddressLinkAsync(Guid tenantId, Guid supplierId, Guid supplierAddressId)
    {
        var association = await _repository.GetAddressAssociationByIdAsync(tenantId, supplierAddressId)
            ?? throw new KeyNotFoundException("Supplier address association not found.");

        if (association.SupplierId != supplierId)
            throw new InvalidOperationException("Supplier address does not belong to this supplier.");

        association.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<SupplierContact>> GetContactsAsync(Guid tenantId, Guid supplierId)
    {
        return await _repository.GetContactsAsync(tenantId, supplierId);
    }

    public async Task<SupplierContact> LinkContactAsync(Guid tenantId, Guid supplierId, SupplierContactLinkRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var supplier = await _repository.GetSupplierByIdAsync(tenantId, supplierId)
            ?? throw new KeyNotFoundException("Supplier not found.");

        var contact = await _addressRepository.GetContactByIdAsync(tenantId, request.ContactId);
        if (contact == null)
            throw new InvalidOperationException("Contact not found or does not belong to the current tenant.");

        var contactType = await _addressRepository.GetContactTypeByIdAsync(tenantId, request.ContactTypeId);
        if (contactType == null)
            throw new InvalidOperationException("ContactType not found or does not belong to the current tenant.");

        var existing = await _repository.GetContactAssociationAsync(tenantId, supplierId, request.ContactId, request.ContactTypeId);
        if (existing != null)
            throw new InvalidOperationException("This contact association already exists for this supplier.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        if (request.IsPrimary)
        {
            var activePrimaries = (await _repository.GetContactsAsync(tenantId, supplierId))
                .Where(x => x.ContactTypeId == request.ContactTypeId && x.IsPrimary && x.IsActive)
                .ToList();

            foreach (var p in activePrimaries)
            {
                p.SetAsPrimary(false);
            }
            await _repository.SaveChangesAsync();
        }

        var association = SupplierContact.Create(
            tenantId,
            supplierId,
            request.ContactId,
            request.ContactTypeId,
            request.IsPrimary);

        await _repository.AddContactAssociationAsync(association);
        await _repository.SaveChangesAsync();

        if (request.IsPrimary)
        {
            supplier.SyncPrimaryContact(contact.ContactName, contact.Email, contact.Phone);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return association;
    }

    public async Task SetPrimaryContactAsync(Guid tenantId, Guid supplierId, Guid supplierContactId)
    {
        var supplier = await _repository.GetSupplierByIdAsync(tenantId, supplierId)
            ?? throw new KeyNotFoundException("Supplier not found.");

        var association = await _repository.GetContactAssociationByIdAsync(tenantId, supplierContactId)
            ?? throw new KeyNotFoundException("Supplier contact association not found.");

        if (association.SupplierId != supplierId)
            throw new InvalidOperationException("Supplier contact does not belong to this supplier.");

        using var transaction = await _context.Database.BeginTransactionAsync();

        var activePrimaries = (await _repository.GetContactsAsync(tenantId, supplierId))
            .Where(x => x.ContactTypeId == association.ContactTypeId && x.IsPrimary && x.IsActive)
            .ToList();

        foreach (var p in activePrimaries)
        {
            p.SetAsPrimary(false);
        }
        await _repository.SaveChangesAsync();

        association.SetAsPrimary(true);
        await _repository.SaveChangesAsync();

        var contact = await _addressRepository.GetContactByIdAsync(tenantId, association.ContactId);
        if (contact != null)
        {
            supplier.SyncPrimaryContact(contact.ContactName, contact.Email, contact.Phone);
            await _repository.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task RemoveContactLinkAsync(Guid tenantId, Guid supplierId, Guid supplierContactId)
    {
        var association = await _repository.GetContactAssociationByIdAsync(tenantId, supplierContactId)
            ?? throw new KeyNotFoundException("Supplier contact association not found.");

        if (association.SupplierId != supplierId)
            throw new InvalidOperationException("Supplier contact does not belong to this supplier.");

        association.Deactivate();
        await _repository.SaveChangesAsync();
    }
}
