using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Address;

public interface IAddressService
{
    Task<List<Address>> GetAddressesAsync(Guid tenantId);
    Task<Address?> GetAddressAsync(Guid tenantId, Guid addressId);
    Task<Address> CreateAddressAsync(Guid tenantId, CreateAddressRequest request);
    Task<Address> UpdateAddressAsync(Guid tenantId, Guid addressId, UpdateAddressRequest request);
    Task<Address> ResolveCountryAsync(Guid tenantId, Guid addressId, Guid countryId);
    Task DeactivateAddressAsync(Guid tenantId, Guid addressId);

    Task<List<Contact.Contact>> GetContactsAsync(Guid tenantId);
    Task<Contact.Contact?> GetContactAsync(Guid tenantId, Guid contactId);
    Task<Contact.Contact> CreateContactAsync(Guid tenantId, CreateContactRequest request);
    Task<Contact.Contact> UpdateContactAsync(Guid tenantId, Guid contactId, UpdateContactRequest request);
    Task DeactivateContactAsync(Guid tenantId, Guid contactId);

    Task<List<AddressType.AddressType>> GetAddressTypesAsync(Guid tenantId);
    Task<AddressType.AddressType> CreateAddressTypeAsync(Guid tenantId, CreateAddressTypeRequest request);

    Task<List<Contact.ContactType>> GetContactTypesAsync(Guid tenantId);
    Task<Contact.ContactType> CreateContactTypeAsync(Guid tenantId, CreateContactTypeRequest request);

    Task<List<Country.Country>> GetCountriesAsync();
    Task<Country.Country?> GetCountryByIdAsync(Guid countryId);
}
