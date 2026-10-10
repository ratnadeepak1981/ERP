using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Address;

public class AddressService : IAddressService
{
    private readonly IAddressRepository _repository;

    public AddressService(IAddressRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Address>> GetAddressesAsync(Guid tenantId)
    {
        return await _repository.GetAddressesAsync(tenantId);
    }

    public async Task<Address?> GetAddressAsync(Guid tenantId, Guid addressId)
    {
        return await _repository.GetAddressByIdAsync(tenantId, addressId);
    }

    public async Task<Address> CreateAddressAsync(Guid tenantId, CreateAddressRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.AddressName))
            throw new ArgumentException("Address name is required.", nameof(request.AddressName));

        if (string.IsNullOrWhiteSpace(request.AddressLine1))
            throw new ArgumentException("Address line 1 is required.", nameof(request.AddressLine1));

        if (string.IsNullOrWhiteSpace(request.City))
            throw new ArgumentException("City is required.", nameof(request.City));

        Guid? countryId = null;
        string? unmatchedCountryText = request.UnmatchedCountryText?.Trim();

        if (request.CountryId.HasValue)
        {
            var country = await _repository.GetCountryByIdAsync(request.CountryId.Value);
            if (country == null)
                throw new InvalidOperationException("Specified CountryId was not found in ISO reference data.");
            countryId = country.CountryId;
            unmatchedCountryText = null;
        }

        var address = Address.Create(
            tenantId,
            request.AddressName.Trim(),
            request.AddressLine1.Trim(),
            request.City.Trim(),
            request.AddressLine2?.Trim(),
            request.StateProvince?.Trim(),
            request.PostalCode?.Trim(),
            countryId,
            unmatchedCountryText);

        await _repository.AddAddressAsync(address);
        await _repository.SaveChangesAsync();
        return address;
    }

    public async Task<Address> UpdateAddressAsync(Guid tenantId, Guid addressId, UpdateAddressRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var address = await _repository.GetAddressByIdAsync(tenantId, addressId)
            ?? throw new KeyNotFoundException("Address not found.");

        if (string.IsNullOrWhiteSpace(request.AddressName))
            throw new ArgumentException("Address name is required.", nameof(request.AddressName));

        if (string.IsNullOrWhiteSpace(request.AddressLine1))
            throw new ArgumentException("Address line 1 is required.", nameof(request.AddressLine1));

        if (string.IsNullOrWhiteSpace(request.City))
            throw new ArgumentException("City is required.", nameof(request.City));

        if (request.CountryId.HasValue)
        {
            var country = await _repository.GetCountryByIdAsync(request.CountryId.Value);
            if (country == null)
                throw new InvalidOperationException("Specified CountryId was not found in ISO reference data.");
        }

        address.Update(
            request.AddressName.Trim(),
            request.AddressLine1.Trim(),
            request.City.Trim(),
            request.AddressLine2?.Trim(),
            request.StateProvince?.Trim(),
            request.PostalCode?.Trim(),
            request.CountryId);

        await _repository.SaveChangesAsync();
        return address;
    }

    public async Task<Address> ResolveCountryAsync(Guid tenantId, Guid addressId, Guid countryId)
    {
        var address = await _repository.GetAddressByIdAsync(tenantId, addressId)
            ?? throw new KeyNotFoundException("Address not found.");

        var country = await _repository.GetCountryByIdAsync(countryId)
            ?? throw new InvalidOperationException("Specified CountryId was not found in ISO reference data.");

        address.ResolveCountry(countryId);
        await _repository.SaveChangesAsync();
        return address;
    }

    public async Task DeactivateAddressAsync(Guid tenantId, Guid addressId)
    {
        var address = await _repository.GetAddressByIdAsync(tenantId, addressId)
            ?? throw new KeyNotFoundException("Address not found.");

        address.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<Contact.Contact>> GetContactsAsync(Guid tenantId)
    {
        return await _repository.GetContactsAsync(tenantId);
    }

    public async Task<Contact.Contact?> GetContactAsync(Guid tenantId, Guid contactId)
    {
        return await _repository.GetContactByIdAsync(tenantId, contactId);
    }

    public async Task<Contact.Contact> CreateContactAsync(Guid tenantId, CreateContactRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ContactName))
            throw new ArgumentException("Contact name is required.", nameof(request.ContactName));

        var contact = Contact.Contact.Create(
            tenantId,
            request.ContactName.Trim(),
            request.JobTitle?.Trim(),
            request.Email?.Trim(),
            request.Phone?.Trim(),
            request.MobileNumber?.Trim(),
            request.PreferredContactMethod?.Trim());

        await _repository.AddContactAsync(contact);
        await _repository.SaveChangesAsync();
        return contact;
    }

    public async Task<Contact.Contact> UpdateContactAsync(Guid tenantId, Guid contactId, UpdateContactRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var contact = await _repository.GetContactByIdAsync(tenantId, contactId)
            ?? throw new KeyNotFoundException("Contact not found.");

        if (string.IsNullOrWhiteSpace(request.ContactName))
            throw new ArgumentException("Contact name is required.", nameof(request.ContactName));

        contact.Update(
            request.ContactName.Trim(),
            request.JobTitle?.Trim(),
            request.Email?.Trim(),
            request.Phone?.Trim(),
            request.MobileNumber?.Trim(),
            request.PreferredContactMethod?.Trim());

        await _repository.SaveChangesAsync();
        return contact;
    }

    public async Task DeactivateContactAsync(Guid tenantId, Guid contactId)
    {
        var contact = await _repository.GetContactByIdAsync(tenantId, contactId)
            ?? throw new KeyNotFoundException("Contact not found.");

        contact.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<List<AddressType.AddressType>> GetAddressTypesAsync(Guid tenantId)
    {
        return await _repository.GetAddressTypesAsync(tenantId);
    }

    public async Task<AddressType.AddressType> CreateAddressTypeAsync(Guid tenantId, CreateAddressTypeRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Address type code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Address type name is required.", nameof(request.Name));

        string code = request.Code.Trim().ToUpperInvariant();
        var existing = await _repository.GetAddressTypeByCodeAsync(tenantId, code);
        if (existing != null)
            throw new InvalidOperationException($"Address type with code '{code}' already exists.");

        var addressType = AddressType.AddressType.Create(
            tenantId,
            code,
            request.Name.Trim(),
            request.Description?.Trim());

        await _repository.AddAddressTypeAsync(addressType);
        await _repository.SaveChangesAsync();
        return addressType;
    }

    public async Task<List<Contact.ContactType>> GetContactTypesAsync(Guid tenantId)
    {
        return await _repository.GetContactTypesAsync(tenantId);
    }

    public async Task<Contact.ContactType> CreateContactTypeAsync(Guid tenantId, CreateContactTypeRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Contact type code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Contact type name is required.", nameof(request.Name));

        string code = request.Code.Trim().ToUpperInvariant();
        var existing = await _repository.GetContactTypeByCodeAsync(tenantId, code);
        if (existing != null)
            throw new InvalidOperationException($"Contact type with code '{code}' already exists.");

        var contactType = Contact.ContactType.Create(
            tenantId,
            code,
            request.Name.Trim(),
            request.Description?.Trim());

        await _repository.AddContactTypeAsync(contactType);
        await _repository.SaveChangesAsync();
        return contactType;
    }

    public async Task<List<Country.Country>> GetCountriesAsync()
    {
        return await _repository.GetCountriesAsync();
    }

    public async Task<Country.Country?> GetCountryByIdAsync(Guid countryId)
    {
        return await _repository.GetCountryByIdAsync(countryId);
    }
}
